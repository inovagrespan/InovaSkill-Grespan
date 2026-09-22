using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record NearbyBuildingCoordinate(
    decimal Latitude,
    decimal Longitude,
    string PlaceId,
    string DisplayName,
    decimal DistanceMeters,
    string Source);

public interface INearbyBuildingCoordinateProvider
{
    Task<IReadOnlyList<NearbyBuildingCoordinate>> DiscoverCityAddressesAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NearbyBuildingCoordinate>>([]);

    Task<NearbyBuildingCoordinate?> FindAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        CancellationToken cancellationToken);

    Task<NearbyBuildingCoordinate?> FindAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        IReadOnlySet<string> excludedCoordinateKeys,
        CancellationToken cancellationToken) => FindAsync(latitude, longitude, city, stateCode, cancellationToken);

    Task<NearbyBuildingCoordinate?> FindCityAddressAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        IReadOnlySet<string> excludedCoordinateKeys,
        CancellationToken cancellationToken) => Task.FromResult<NearbyBuildingCoordinate?>(null);

    Task<NearbyBuildingCoordinate?> FindOpenStreetMapBuildingAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        IReadOnlySet<string> excludedCoordinateKeys,
        CancellationToken cancellationToken) => Task.FromResult<NearbyBuildingCoordinate?>(null);

    Task<NearbyBuildingCoordinate?> FindAlternateCityAddressAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        IReadOnlySet<string> excludedCoordinateKeys,
        CancellationToken cancellationToken) => Task.FromResult<NearbyBuildingCoordinate?>(null);
}

public sealed class GoogleNearbyRooftopCoordinateProvider(
    HttpClient httpClient,
    IOptions<GoogleGeocodingOptions> options,
    IGoogleGeocodingRequestGate requestGate) : INearbyBuildingCoordinateProvider
{
    public const string SimulatedSource = "GOOGLE_SIMULATED_NEARBY";
    public const decimal MaximumDistanceMeters = 5_000m;
    private const double EarthRadiusMeters = 6_371_000d;
    private const decimal ProbeRadiusMeters = 2_500m;
    private const string ResponseFieldMask =
        "results.placeId,results.location,results.granularity,results.formattedAddress," +
        "results.addressComponents,results.types";
    private static readonly int[] ProbeBearings = [0, 45, 90, 135, 180, 225, 270, 315];

    public async Task<NearbyBuildingCoordinate?> FindAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        CancellationToken cancellationToken)
        => await FindAsync(latitude, longitude, city, stateCode, new HashSet<string>(), cancellationToken);

    public async Task<NearbyBuildingCoordinate?> FindAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        IReadOnlySet<string> excludedCoordinateKeys,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new InvalidOperationException("GOOGLE_MAPS_API_KEY não foi configurada.");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return null;

        NearbyBuildingCoordinate? best = null;
        foreach (var probe in ProbePoints(latitude, longitude))
        {
            var response = await ReverseAsync(probe.Latitude, probe.Longitude, cancellationToken);
            foreach (var result in response.Results ?? [])
            {
                if (!string.Equals(result.Granularity, "ROOFTOP", StringComparison.OrdinalIgnoreCase) ||
                    !(result.Types ?? []).Any(type => string.Equals(type, "street_address", StringComparison.OrdinalIgnoreCase)) ||
                    result.Location is null || string.IsNullOrWhiteSpace(result.PlaceId) ||
                    string.IsNullOrWhiteSpace(result.FormattedAddress) ||
                    !SameText(ReadComponent(result, "locality") ??
                        ReadComponent(result, "administrative_area_level_2"), city) ||
                    !string.Equals(ReadComponent(result, "administrative_area_level_1", true), stateCode,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (excludedCoordinateKeys.Contains(CoordinateKey(result.Location.Latitude, result.Location.Longitude)))
                    continue;

                var distance = DistanceMeters(latitude, longitude,
                    result.Location.Latitude, result.Location.Longitude);
                if (distance > MaximumDistanceMeters) continue;
                var candidate = new NearbyBuildingCoordinate(result.Location.Latitude,
                    result.Location.Longitude, result.PlaceId, result.FormattedAddress, distance,
                    SimulatedSource);
                if (best is null || candidate.DistanceMeters < best.DistanceMeters) best = candidate;
            }
            if (best is not null) break;
        }
        return best;
    }

    public static string CoordinateKey(decimal latitude, decimal longitude) =>
        $"{decimal.Round(latitude, 6):0.000000}|{decimal.Round(longitude, 6):0.000000}";

    internal static decimal DistanceMeters(decimal latitude1, decimal longitude1,
        decimal latitude2, decimal longitude2)
    {
        var lat1 = DegreesToRadians((double)latitude1);
        var lat2 = DegreesToRadians((double)latitude2);
        var deltaLatitude = DegreesToRadians((double)(latitude2 - latitude1));
        var deltaLongitude = DegreesToRadians((double)(longitude2 - longitude1));
        var value = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2) +
                    Math.Cos(lat1) * Math.Cos(lat2) *
                    Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
        var distance = EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(value), Math.Sqrt(1 - value));
        return decimal.Round((decimal)distance, 3, MidpointRounding.AwayFromZero);
    }

    private async Task<GoogleGeocodeResponse> ReverseAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken)
    {
        var path = $"geocode/location/{latitude.ToString(CultureInfo.InvariantCulture)}," +
                   $"{longitude.ToString(CultureInfo.InvariantCulture)}?regionCode=BR&languageCode=pt-BR" +
                   "&granularity=ROOFTOP&types=street_address";
        var maximumRetries = Math.Max(0, options.Value.RateLimitMaximumRetries);
        for (var retry = 0; ; retry++)
        {
            await requestGate.WaitAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Goog-Api-Key", options.Value.ApiKey);
            request.Headers.Add("X-Goog-FieldMask", ResponseFieldMask);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && retry < maximumRetries)
            {
                var seconds = Math.Min(Math.Max(1, options.Value.RateLimitMaximumDelaySeconds),
                    Math.Max(1, options.Value.RateLimitFallbackDelaySeconds) * Math.Pow(2, retry));
                await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
                continue;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GoogleGeocodeResponse>(cancellationToken)
                   ?? new GoogleGeocodeResponse([]);
        }
    }

    private static IEnumerable<(decimal Latitude, decimal Longitude)> ProbePoints(
        decimal latitude,
        decimal longitude)
    {
        yield return (latitude, longitude);
        foreach (var bearing in ProbeBearings)
            yield return Offset(latitude, longitude, ProbeRadiusMeters, bearing);
    }

    private static (decimal Latitude, decimal Longitude) Offset(
        decimal latitude,
        decimal longitude,
        decimal distanceMeters,
        int bearingDegrees)
    {
        var angularDistance = (double)distanceMeters / EarthRadiusMeters;
        var bearing = DegreesToRadians(bearingDegrees);
        var lat1 = DegreesToRadians((double)latitude);
        var lon1 = DegreesToRadians((double)longitude);
        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(angularDistance) +
                             Math.Cos(lat1) * Math.Sin(angularDistance) * Math.Cos(bearing));
        var lon2 = lon1 + Math.Atan2(Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(lat1),
            Math.Cos(angularDistance) - Math.Sin(lat1) * Math.Sin(lat2));
        return ((decimal)(lat2 * 180d / Math.PI), (decimal)(lon2 * 180d / Math.PI));
    }

    private static string? ReadComponent(GoogleGeocodeResult result, string type, bool shortText = false)
    {
        var component = (result.AddressComponents ?? []).FirstOrDefault(item =>
            (item.Types ?? []).Any(itemType => string.Equals(itemType, type, StringComparison.OrdinalIgnoreCase)));
        return shortText ? component?.ShortText ?? component?.LongText : component?.LongText ?? component?.ShortText;
    }

    private static bool SameText(string? left, string? right) => Normalize(left) == Normalize(right);
    private static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant();
    }
    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private sealed record GoogleGeocodeResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<GoogleGeocodeResult>? Results);
    private sealed record GoogleGeocodeResult(
        [property: JsonPropertyName("placeId")] string? PlaceId,
        [property: JsonPropertyName("location")] GoogleLocation? Location,
        [property: JsonPropertyName("granularity")] string? Granularity,
        [property: JsonPropertyName("formattedAddress")] string? FormattedAddress,
        [property: JsonPropertyName("addressComponents")] IReadOnlyList<GoogleAddressComponent>? AddressComponents,
        [property: JsonPropertyName("types")] IReadOnlyList<string>? Types);
    private sealed record GoogleLocation(
        [property: JsonPropertyName("latitude")] decimal Latitude,
        [property: JsonPropertyName("longitude")] decimal Longitude);
    private sealed record GoogleAddressComponent(
        [property: JsonPropertyName("longText")] string? LongText,
        [property: JsonPropertyName("shortText")] string? ShortText,
        [property: JsonPropertyName("types")] IReadOnlyList<string>? Types);
}
