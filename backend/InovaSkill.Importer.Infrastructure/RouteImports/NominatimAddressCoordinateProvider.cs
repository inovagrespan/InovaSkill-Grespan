using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record AddressCoordinateQuery(string? StreetType, string Street, string? Number, string? Neighborhood,
    string City, string StateCode, string? PostalCode);
public sealed record AddressCoordinateLookup(string Status, decimal? Latitude, decimal? Longitude,
    string? PlaceId, string? DisplayName, string? FailureReason);

public interface ICustomerAddressCoordinateProvider
{
    Task<AddressCoordinateLookup> FindAsync(AddressCoordinateQuery query, CancellationToken cancellationToken);
}

public interface INominatimRequestGate
{
    Task WaitAsync(CancellationToken cancellationToken);
}

public sealed class NominatimRequestGate(IOptions<NominatimOptions> options) : INominatimRequestGate
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    private DateTime lastRequestStartedAt = DateTime.MinValue;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            var interval = TimeSpan.FromMilliseconds(Math.Max(1000, options.Value.MinimumRequestIntervalMilliseconds));
            var remaining = interval - (DateTime.UtcNow - lastRequestStartedAt);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
            lastRequestStartedAt = DateTime.UtcNow;
        }
        finally { mutex.Release(); }
    }
}

public sealed class NominatimAddressCoordinateProvider(
    HttpClient httpClient,
    INominatimRequestGate requestGate) : ICustomerAddressCoordinateProvider
{
    public async Task<AddressCoordinateLookup> FindAsync(AddressCoordinateQuery query, CancellationToken cancellationToken)
    {
        await requestGate.WaitAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(query.Street) || string.IsNullOrWhiteSpace(query.Number))
            return new("NOT_FOUND", null, null, null, null,
                "Logradouro e número são obrigatórios para confirmar uma coordenada exata.");
        var street = FormatStreet(query.StreetType, query.Street);
        var postalCode = FormatPostalCode(query.PostalCode);
        var address = string.Join(", ", new[] { street, query.Number, query.Neighborhood, query.City,
            query.StateCode, postalCode, "Brasil" }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var url = "search?q=" + Uri.EscapeDataString(address) +
            "&format=json&addressdetails=1&countrycodes=br&limit=1";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta ??
                (response.Headers.RetryAfter?.Date is { } retryAt
                    ? retryAt - DateTimeOffset.UtcNow
                    : null);
            if (retryAfter > TimeSpan.Zero) await Task.Delay(retryAfter.Value, cancellationToken);
            throw new NominatimRateLimitException(retryAfter);
        }
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<NominatimResult[]>(cancellationToken) ?? [];
        var result = results.FirstOrDefault();
        if (result is null) return new("NOT_FOUND", null, null, null, null, "Endereço não encontrado.");

        var returnedCity = result.Address?.City ?? result.Address?.Town ?? result.Address?.Municipality ?? result.Address?.Village;
        var returnedStateCode = result.Address?.StateCode?.Split('-').LastOrDefault();
        if (!SameText(returnedCity, query.City) ||
            !string.Equals(returnedStateCode, query.StateCode, StringComparison.OrdinalIgnoreCase))
            return new("NOT_FOUND", null, null, result.PlaceId?.ToString(CultureInfo.InvariantCulture), result.DisplayName,
                "Resultado incompatível com o município ou a UF consultados.");
        if (!SameText(result.Address?.HouseNumber, query.Number))
            return new("NOT_FOUND", null, null, result.PlaceId?.ToString(CultureInfo.InvariantCulture), result.DisplayName,
                "O Nominatim não confirmou o número do imóvel.");
        if (!decimal.TryParse(result.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !decimal.TryParse(result.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            return new("FAILED", null, null, result.PlaceId?.ToString(CultureInfo.InvariantCulture), result.DisplayName, "Coordenadas inválidas no retorno.");
        return new("RESOLVED", latitude, longitude, result.PlaceId?.ToString(CultureInfo.InvariantCulture), result.DisplayName, null);
    }

    private static bool SameText(string? left, string right) => Normalize(left) == Normalize(right);
    public static string FormatStreet(string? streetType, string street)
    {
        var cleanStreet = street.Trim();
        var cleanType = streetType?.Trim();
        if (string.IsNullOrWhiteSpace(cleanType) || Normalize(cleanStreet).StartsWith(Normalize(cleanType) + " "))
            return cleanStreet;
        return $"{cleanType} {cleanStreet}";
    }

    public static string? FormatPostalCode(string? postalCode)
    {
        var digits = new string((postalCode ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? $"{digits[..5]}-{digits[5..]}" :
            string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
    }
    private static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant();
    }

    private sealed record NominatimResult(
        [property: JsonPropertyName("place_id")] long? PlaceId,
        [property: JsonPropertyName("lat")] string? Latitude,
        [property: JsonPropertyName("lon")] string? Longitude,
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("address")] NominatimAddress? Address);
    private sealed record NominatimAddress(
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("town")] string? Town,
        [property: JsonPropertyName("municipality")] string? Municipality,
        [property: JsonPropertyName("village")] string? Village,
        [property: JsonPropertyName("house_number")] string? HouseNumber,
        [property: JsonPropertyName("ISO3166-2-lvl4")] string? StateCode);
}

public sealed class NominatimRateLimitException(TimeSpan? retryAfter)
    : Exception("O Nominatim limitou temporariamente as requisições.")
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
