using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class BrasilApiCustomerRegistrationAddressProvider(
    HttpClient httpClient,
    IOptions<BrasilApiOptions> options) : ICustomerRegistrationAddressProvider
{
    private readonly BrasilApiOptions settings = options.Value;
    private readonly SemaphoreSlim rateLimitLock = new(1, 1);
    private DateTime nextRequestAt = DateTime.MinValue;

    public async Task<CustomerRegistrationAddressLookup> FindByCnpjAsync(
        string cnpj,
        CancellationToken cancellationToken)
    {
        var normalizedCnpj = new string(cnpj.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalizedCnpj.Length != 14)
        {
            return new CustomerRegistrationAddressLookup(
                CustomerRegistrationAddressStatuses.InvalidDocument,
                FailureReason: "CNPJ deve conter 14 caracteres.");
        }

        var maximumRetries = Math.Max(0, settings.RateLimitMaximumRetries);
        for (var retry = 0; ; retry++)
        {
            await WaitForRateLimitAsync(cancellationToken);
            using var response = await httpClient.GetAsync(
                $"cnpj/v1/{Uri.EscapeDataString(normalizedCnpj)}",
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (retry >= maximumRetries)
                    throw new BrasilApiRateLimitException(normalizedCnpj, retry + 1);
                await Task.Delay(CalculateRateLimitDelay(response, retry), cancellationToken);
                continue;
            }
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new CustomerRegistrationAddressLookup(
                    CustomerRegistrationAddressStatuses.InvalidDocument,
                    FailureReason: "A BrasilAPI rejeitou o CNPJ informado.");
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new CustomerRegistrationAddressLookup(
                    CustomerRegistrationAddressStatuses.NotFound,
                    FailureReason: "CNPJ não encontrado na BrasilAPI.");
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<BrasilApiCnpjResponse>(cancellationToken)
                ?? throw new HttpRequestException("A BrasilAPI retornou uma resposta vazia.");
            var lookup = new CustomerRegistrationAddressLookup(
                CustomerRegistrationAddressStatuses.Resolved,
                Clean(payload.PostalCode),
                Clean(payload.StateCode),
                Clean(payload.City),
                Clean(payload.Street),
                Clean(payload.Number),
                Clean(payload.Complement),
                Clean(payload.Neighborhood),
                Clean(payload.StreetType));
            return string.IsNullOrWhiteSpace(lookup.Street) || string.IsNullOrWhiteSpace(lookup.Neighborhood)
                ? await ComplementFromPostalCodeAsync(lookup, cancellationToken)
                : lookup;
        }
    }

    private async Task<CustomerRegistrationAddressLookup> ComplementFromPostalCodeAsync(
        CustomerRegistrationAddressLookup current, CancellationToken cancellationToken)
    {
        var digits = new string((current.PostalCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != 8) return current with
        {
            PostalCodeEnrichmentStatus = PostalCodeEnrichmentStatuses.NotFound,
            FailureReason = "O endereço cadastral está incompleto e não possui CEP válido para complementação."
        };

        try
        {
            await WaitForRateLimitAsync(cancellationToken);
            using var response = await httpClient.GetAsync($"cep/v2/{digits}", cancellationToken);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                return current with { PostalCodeEnrichmentStatus = PostalCodeEnrichmentStatuses.NotFound };
            response.EnsureSuccessStatusCode();
            var postal = await response.Content.ReadFromJsonAsync<BrasilApiPostalCodeResponse>(cancellationToken);
            if (postal is null) throw new HttpRequestException("A BrasilAPI retornou uma resposta de CEP vazia.");
            if (!SameText(postal.City, current.City) ||
                !string.Equals(Clean(postal.StateCode), current.StateCode, StringComparison.OrdinalIgnoreCase))
                return current with { PostalCodeEnrichmentStatus = PostalCodeEnrichmentStatuses.Incompatible };

            var street = Clean(current.Street) ?? Clean(postal.Street);
            var neighborhood = Clean(current.Neighborhood) ?? Clean(postal.Neighborhood);
            var complemented = !string.Equals(street, current.Street, StringComparison.Ordinal) ||
                !string.Equals(neighborhood, current.Neighborhood, StringComparison.Ordinal);
            return current with
            {
                Street = street,
                Neighborhood = neighborhood,
                Source = complemented ? "BRASIL_API_CNPJ_CEP" : current.Source,
                PostalCodeEnrichmentStatus = complemented
                    ? PostalCodeEnrichmentStatuses.Complemented
                    : PostalCodeEnrichmentStatuses.NotFound
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            return current with
            {
                PostalCodeEnrichmentStatus = PostalCodeEnrichmentStatuses.TechnicalFailure,
                FailureReason = $"Falha ao complementar o endereço pelo CEP: {exception.Message}"
            };
        }
    }

    private TimeSpan CalculateRateLimitDelay(HttpResponseMessage response, int retry)
    {
        var configuredMaximum = TimeSpan.FromSeconds(Math.Max(0, settings.RateLimitMaximumDelaySeconds));
        var retryAfter = response.Headers.RetryAfter?.Delta ??
            (response.Headers.RetryAfter?.Date is { } retryAt
                ? retryAt - DateTimeOffset.UtcNow
                : null);
        var fallbackSeconds = Math.Max(0, settings.RateLimitFallbackDelaySeconds) * Math.Pow(2, retry);
        var delay = retryAfter is { } serverDelay && serverDelay > TimeSpan.Zero
            ? serverDelay
            : TimeSpan.FromSeconds(fallbackSeconds);
        if (configuredMaximum > TimeSpan.Zero && delay > configuredMaximum)
            delay = configuredMaximum;
        var jitterMaximum = Math.Max(0, settings.RateLimitJitterMaximumMilliseconds);
        return delay + TimeSpan.FromMilliseconds(jitterMaximum == 0 ? 0 : Random.Shared.Next(jitterMaximum + 1));
    }

    private async Task WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        var requestsPerSecond = Math.Max(1, settings.RequestsPerSecond);
        var interval = TimeSpan.FromSeconds(1d / requestsPerSecond);
        await rateLimitLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            if (nextRequestAt > now)
                await Task.Delay(nextRequestAt - now, cancellationToken);
            nextRequestAt = DateTime.UtcNow.Add(interval);
        }
        finally
        {
            rateLimitLock.Release();
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool SameText(string? left, string? right) =>
        NormalizeText(left) == NormalizeText(right);

    private static string NormalizeText(string? value) => string.Concat((value ?? string.Empty)
        .Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
        .Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant();

    private sealed record BrasilApiCnpjResponse(
        [property: JsonPropertyName("cep")] string? PostalCode,
        [property: JsonPropertyName("uf")] string? StateCode,
        [property: JsonPropertyName("municipio")] string? City,
        [property: JsonPropertyName("logradouro")] string? Street,
        [property: JsonPropertyName("descricao_tipo_de_logradouro")] string? StreetType,
        [property: JsonPropertyName("numero")] string? Number,
        [property: JsonPropertyName("complemento")] string? Complement,
        [property: JsonPropertyName("bairro")] string? Neighborhood);

    private sealed record BrasilApiPostalCodeResponse(
        [property: JsonPropertyName("state")] string? StateCode,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("street")] string? Street,
        [property: JsonPropertyName("neighborhood")] string? Neighborhood);
}

public sealed class BrasilApiRateLimitException(string cnpj, int attempts)
    : HttpRequestException($"A BrasilAPI manteve o limite de requisições para o CNPJ {cnpj} após {attempts} tentativa(s).")
{
}
