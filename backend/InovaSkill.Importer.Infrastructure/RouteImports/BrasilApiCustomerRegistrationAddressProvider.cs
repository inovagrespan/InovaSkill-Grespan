using System.Net;
using System.Net.Http.Json;
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
            return new CustomerRegistrationAddressLookup(
                CustomerRegistrationAddressStatuses.Resolved,
                Clean(payload.PostalCode),
                Clean(payload.StateCode),
                Clean(payload.City),
                Clean(payload.Street),
                Clean(payload.Number),
                Clean(payload.Complement),
                Clean(payload.Neighborhood));
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

    private sealed record BrasilApiCnpjResponse(
        [property: JsonPropertyName("cep")] string? PostalCode,
        [property: JsonPropertyName("uf")] string? StateCode,
        [property: JsonPropertyName("municipio")] string? City,
        [property: JsonPropertyName("logradouro")] string? Street,
        [property: JsonPropertyName("numero")] string? Number,
        [property: JsonPropertyName("complemento")] string? Complement,
        [property: JsonPropertyName("bairro")] string? Neighborhood);
}

public sealed class BrasilApiRateLimitException(string cnpj, int attempts)
    : HttpRequestException($"A BrasilAPI manteve o limite de requisições para o CNPJ {cnpj} após {attempts} tentativa(s).")
{
}
