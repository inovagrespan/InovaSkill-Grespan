using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public interface IGoogleGeocodingRequestGate
{
    Task WaitAsync(CancellationToken cancellationToken);
}

public sealed class GoogleGeocodingRequestGate(IOptions<GoogleGeocodingOptions> options)
    : IGoogleGeocodingRequestGate
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    private DateTime lastRequestStartedAt = DateTime.MinValue;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            var interval = TimeSpan.FromMilliseconds(Math.Max(1, options.Value.MinimumRequestIntervalMilliseconds));
            var remaining = interval - (DateTime.UtcNow - lastRequestStartedAt);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
            lastRequestStartedAt = DateTime.UtcNow;
        }
        finally { mutex.Release(); }
    }
}

public sealed class GoogleAddressCoordinateProvider(
    HttpClient httpClient,
    IOptions<GoogleGeocodingOptions> options,
    IGoogleGeocodingRequestGate requestGate) : ICustomerAddressCoordinateProvider
{
    private const string RooftopGranularity = "ROOFTOP";
    private const string StreetAddressType = "street_address";
    private const string StreetNumberType = "street_number";
    private const string LocalityType = "locality";
    private const string AdministrativeAreaType = "administrative_area_level_1";
    private const string ResponseFieldMask =
        "results.placeId,results.location,results.granularity,results.formattedAddress," +
        "results.addressComponents,results.types";

    public string SourceName => "GOOGLE_GEOCODING";

    public async Task<AddressCoordinateLookup> FindAsync(
        AddressCoordinateQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new InvalidOperationException("GOOGLE_MAPS_API_KEY não foi configurada.");
        if (string.IsNullOrWhiteSpace(query.Street) || string.IsNullOrWhiteSpace(query.Number))
            return NotFound("Logradouro e número são obrigatórios para confirmar uma coordenada exata.");

        var address = string.Join(", ", new[]
        {
            NominatimAddressCoordinateProvider.FormatStreet(query.StreetType, query.Street),
            query.Number,
            query.Neighborhood,
            query.City,
            query.StateCode,
            NominatimAddressCoordinateProvider.FormatPostalCode(query.PostalCode),
            "Brasil"
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var requestUrl = $"geocode/address/{Uri.EscapeDataString(address)}?regionCode=BR&languageCode=pt-BR";
        using var response = await SendWithRateLimitRetryAsync(requestUrl, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GoogleGeocodeResponse>(cancellationToken)
            ?? new GoogleGeocodeResponse([]);
        var result = payload.Results?.FirstOrDefault(IsExactStreetAddress);
        if (result is null)
            return NotFound("O Google não confirmou uma coordenada predial exata para o endereço.");

        var returnedNumber = ReadComponent(result, StreetNumberType);
        var returnedCity = ReadComponent(result, LocalityType);
        var returnedState = ReadComponent(result, AdministrativeAreaType, preferShortText: true);
        if (!SameText(returnedNumber, query.Number))
            return NotFound("O Google não confirmou o número do imóvel.", result);
        if (!SameText(returnedCity, query.City) ||
            !string.Equals(returnedState, query.StateCode, StringComparison.OrdinalIgnoreCase))
            return NotFound("Resultado incompatível com o município ou a UF consultados.", result);
        if (result.Location is null || !IsValidLatitude(result.Location.Latitude) || !IsValidLongitude(result.Location.Longitude))
            return new("FAILED", null, null, result.PlaceId, result.FormattedAddress, "Coordenadas inválidas no retorno do Google.");

        return new("RESOLVED", result.Location.Latitude, result.Location.Longitude,
            result.PlaceId, result.FormattedAddress, null);
    }

    private async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(
        string requestUrl,
        CancellationToken cancellationToken)
    {
        var maximumRetries = Math.Max(0, options.Value.RateLimitMaximumRetries);
        for (var retry = 0; ; retry++)
        {
            await requestGate.WaitAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("X-Goog-Api-Key", options.Value.ApiKey);
            request.Headers.Add("X-Goog-FieldMask", ResponseFieldMask);
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || retry >= maximumRetries)
                return response;

            var retryAfter = response.Headers.RetryAfter?.Delta ??
                (response.Headers.RetryAfter?.Date is { } retryAt ? retryAt - DateTimeOffset.UtcNow : null);
            response.Dispose();
            var fallbackSeconds = Math.Max(1, options.Value.RateLimitFallbackDelaySeconds);
            var maximumDelaySeconds = Math.Max(fallbackSeconds, options.Value.RateLimitMaximumDelaySeconds);
            var exponentialSeconds = Math.Min(maximumDelaySeconds, fallbackSeconds * Math.Pow(2, retry));
            var delay = retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero
                ? retryAfter.Value
                : TimeSpan.FromSeconds(exponentialSeconds);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool IsExactStreetAddress(GoogleGeocodeResult result) =>
        string.Equals(result.Granularity, RooftopGranularity, StringComparison.OrdinalIgnoreCase) &&
        result.Types.Any(type => string.Equals(type, StreetAddressType, StringComparison.OrdinalIgnoreCase));

    private static AddressCoordinateLookup NotFound(string reason, GoogleGeocodeResult? result = null) =>
        new("NOT_FOUND", null, null, result?.PlaceId, result?.FormattedAddress, reason);

    private static string? ReadComponent(
        GoogleGeocodeResult result,
        string type,
        bool preferShortText = false)
    {
        var component = result.AddressComponents.FirstOrDefault(item =>
            item.Types.Any(itemType => string.Equals(itemType, type, StringComparison.OrdinalIgnoreCase)));
        return preferShortText ? component?.ShortText ?? component?.LongText : component?.LongText ?? component?.ShortText;
    }

    private static bool SameText(string? left, string? right) => Normalize(left) == Normalize(right);

    private static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant();
    }

    private static bool IsValidLatitude(decimal latitude) => latitude is >= -90 and <= 90;
    private static bool IsValidLongitude(decimal longitude) => longitude is >= -180 and <= 180;

    private sealed record GoogleGeocodeResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<GoogleGeocodeResult>? Results);
    private sealed record GoogleGeocodeResult(
        [property: JsonPropertyName("placeId")] string? PlaceId,
        [property: JsonPropertyName("location")] GoogleLocation? Location,
        [property: JsonPropertyName("granularity")] string? Granularity,
        [property: JsonPropertyName("formattedAddress")] string? FormattedAddress,
        [property: JsonPropertyName("addressComponents")] IReadOnlyList<GoogleAddressComponent> AddressComponents,
        [property: JsonPropertyName("types")] IReadOnlyList<string> Types);
    private sealed record GoogleLocation(
        [property: JsonPropertyName("latitude")] decimal Latitude,
        [property: JsonPropertyName("longitude")] decimal Longitude);
    private sealed record GoogleAddressComponent(
        [property: JsonPropertyName("longText")] string? LongText,
        [property: JsonPropertyName("shortText")] string? ShortText,
        [property: JsonPropertyName("types")] IReadOnlyList<string> Types);
}
