using System.Globalization;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public interface IGeoapifyRequestGate
{
    Task WaitAsync(CancellationToken cancellationToken);
}

public sealed class GeoapifyRequestGate(IOptions<GeoapifyOptions> options) : IGeoapifyRequestGate
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private DateTimeOffset nextRequestAt = DateTimeOffset.MinValue;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var delay = nextRequestAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            nextRequestAt = DateTimeOffset.UtcNow.AddMilliseconds(
                Math.Max(0, options.Value.MinimumRequestIntervalMilliseconds));
        }
        finally
        {
            semaphore.Release();
        }
    }
}

public sealed class GeoapifyNearbyBuildingCoordinateProvider(
    HttpClient httpClient,
    IOptions<GeoapifyOptions> options,
    IGeoapifyRequestGate requestGate) : INearbyBuildingCoordinateProvider
{
    public const string SimulatedSource = "GEOAPIFY_SIMULATED_NEARBY";
    public const string CityAddressSource = "GEOAPIFY_CITY_ADDRESS";
    public const decimal MaximumDistanceMeters = 25_000m;
    private const double EarthRadiusMeters = 6_371_000d;
    private const decimal ProbeRadiusMeters = 2_500m;
    // A city can already have hundreds of active simulated points. A small
    // pool would be exhausted before the remaining customers are assigned.
    private const int MinimumCityCandidatePoolSize = 750;
    private const int CityDiscoverySourceTimeoutSeconds = 20;
    private static readonly int[] ProbeBearings = [0, 45, 90, 135, 180, 225, 270, 315];
    private static readonly string[] CityPlaceCategories =
        // Residential buildings are valid fallback addresses too. The broader
        // categories are queried once per municipality and merged by coordinate
        // so a city with many pending customers does not exhaust a commercial
        // only catalog.
        [
            "building.residential", "building.commercial", "building.industrial",
            "building.public_and_civil", "building.transportation",
            "service", "commercial", "catering"
        ];
    // Repetir a consulta com centros determinísticos evita ficar preso aos
    // mesmos 500 resultados mais próximos quando a cidade já possui muitos
    // pontos simulados. Os candidatos continuam validados pela distância ao
    // centro original e pelo município/UF antes de serem aceitos.
    private static readonly (decimal LatitudeOffset, decimal LongitudeOffset)[] CityDiscoveryAnchors =
    [
        (0m, 0m), (0.08m, 0m), (-0.08m, 0m), (0m, 0.08m), (0m, -0.08m)
    ];
    private static readonly string[] OpenStreetMapQueries =
        ["nwr[\"addr:street\"]"];
    private static readonly ConcurrentDictionary<string, NearbyBuildingCoordinate[]> OpenStreetMapCache = new();

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
            throw new InvalidOperationException("GEOAPIFY_API_KEY não foi configurada.");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return null;

        NearbyBuildingCoordinate? best = null;
        foreach (var probe in ProbePoints(latitude, longitude))
        {
            GeoapifyReverseResponse response;
            try { response = await ReverseAsync(probe.Latitude, probe.Longitude, cancellationToken); }
            catch (HttpRequestException) { continue; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { continue; }
            foreach (var result in response.Results ?? [])
            {
                var providerStateCode = result.StateCode ?? result.CountyCode;
                if (!IsAddressResultType(result.ResultType) || result.Latitude is null || result.Longitude is null ||
                    string.IsNullOrWhiteSpace(result.PlaceId) || string.IsNullOrWhiteSpace(result.Formatted) ||
                    !string.Equals(result.CountryCode, "br", StringComparison.OrdinalIgnoreCase) ||
                    !MatchesMunicipality(result, city) ||
                    !string.Equals(providerStateCode, stateCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (excludedCoordinateKeys.Contains(CoordinateKey(result.Latitude.Value, result.Longitude.Value)))
                    continue;

                var distance = DistanceMeters(latitude, longitude,
                    result.Latitude.Value, result.Longitude.Value);
                if (distance > options.Value.MaximumSimulationDistanceMeters) continue;
                var candidate = new NearbyBuildingCoordinate(result.Latitude.Value,
                    result.Longitude.Value, result.PlaceId, result.Formatted, distance, SimulatedSource);
                if (best is null || candidate.DistanceMeters < best.DistanceMeters) best = candidate;
            }
            if (best is not null) break;
        }
        return best;
    }

    public async Task<NearbyBuildingCoordinate?> FindCityAddressAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        IReadOnlySet<string> excludedCoordinateKeys,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new InvalidOperationException("GEOAPIFY_API_KEY não foi configurada.");
        await requestGate.WaitAsync(cancellationToken);
        var path = "v1/geocode/search?text=" + Uri.EscapeDataString($"{city}, {stateCode}, Brasil") +
            "&filter=countrycode:br&limit=50&format=json&apiKey=" + Uri.EscapeDataString(options.Value.ApiKey);
        using var response = await SendWithTransportRetryAsync(new Uri(path, UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<GeoapifyReverseResponse>(cancellationToken)
            ?? new GeoapifyReverseResponse([]);
        return payload.Results?
            .Where(result => result.Latitude is not null && result.Longitude is not null &&
                !string.IsNullOrWhiteSpace(result.PlaceId) && !string.IsNullOrWhiteSpace(result.Formatted) &&
                IsAddressResultType(result.ResultType) && MatchesMunicipality(result, city) &&
                string.Equals(result.StateCode ?? result.CountyCode, stateCode, StringComparison.OrdinalIgnoreCase))
            .Select(result => new NearbyBuildingCoordinate(result.Latitude!.Value, result.Longitude!.Value,
                result.PlaceId!, result.Formatted!, DistanceMeters(latitude, longitude,
                    result.Latitude.Value, result.Longitude.Value), SimulatedSource))
            .Where(candidate => candidate.DistanceMeters <= options.Value.MaximumSimulationDistanceMeters &&
                !excludedCoordinateKeys.Contains(CoordinateKey(candidate.Latitude, candidate.Longitude)))
            .OrderBy(candidate => candidate.DistanceMeters)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<NearbyBuildingCoordinate>> DiscoverCityAddressesAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        CancellationToken cancellationToken)
    {
        var candidates = new List<NearbyBuildingCoordinate>();
        using var geoapifyTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        geoapifyTimeout.CancelAfter(TimeSpan.FromSeconds(CityDiscoverySourceTimeoutSeconds));
        await AddGeoapifyPlaceCandidatesAsync(candidates, latitude, longitude, city, stateCode,
            geoapifyTimeout.Token);
        if (DistinctCandidateCount(candidates) < MinimumCityCandidatePoolSize && !cancellationToken.IsCancellationRequested)
        {
            using var searchTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            searchTimeout.CancelAfter(TimeSpan.FromSeconds(CityDiscoverySourceTimeoutSeconds));
            await AddGeoapifySearchCandidatesAsync(candidates, latitude, longitude, city, stateCode,
                searchTimeout.Token);
        }
        using var photonTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        photonTimeout.CancelAfter(TimeSpan.FromSeconds(CityDiscoverySourceTimeoutSeconds));
        try
        {
            using var response = await httpClient.GetAsync(
                "https://photon.komoot.io/api/?q=" + Uri.EscapeDataString($"{city}, {stateCode}, Brazil") + "&limit=100",
                photonTimeout.Token);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(photonTimeout.Token));
                foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
                {
                    if (!feature.TryGetProperty("geometry", out var geometry) ||
                        !geometry.TryGetProperty("coordinates", out var coordinates) || coordinates.GetArrayLength() < 2)
                        continue;
                    var candidateLongitude = coordinates[0].GetDecimal();
                    var candidateLatitude = coordinates[1].GetDecimal();
                    var properties = feature.TryGetProperty("properties", out var value) ? value : default;
                    var returnedCity = properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty("city", out var cityValue) ? cityValue.GetString() : null;
                    if (!SameText(returnedCity, city)) continue;
                    var street = properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty("street", out var streetValue) ? streetValue.GetString() : null;
                    var number = properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty("housenumber", out var numberValue) ? numberValue.GetString() : null;
                    if (string.IsNullOrWhiteSpace(street) && string.IsNullOrWhiteSpace(number)) continue;
                    var distance = DistanceMeters(latitude, longitude, candidateLatitude, candidateLongitude);
                    if (distance > options.Value.MaximumSimulationDistanceMeters) continue;
                    candidates.Add(new NearbyBuildingCoordinate(candidateLatitude, candidateLongitude,
                        $"photon:{CoordinateKey(candidateLatitude, candidateLongitude)}",
                        string.Join(", ", new[] { street, number, city, stateCode }.Where(item => !string.IsNullOrWhiteSpace(item))),
                        distance, "PHOTON_CITY_ADDRESS"));
                }
            }
        }
        catch (HttpRequestException) { }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        // O Overpass é deliberadamente o último fallback. Consultá-lo sempre tornava
        // um município uma sequência de sete requisições lentas, mesmo quando o
        // catálogo Geoapify/Photon já havia retornado endereços suficientes.
        if (DistinctCandidateCount(candidates) < MinimumCityCandidatePoolSize && !cancellationToken.IsCancellationRequested)
        {
            using var osmTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            osmTimeout.CancelAfter(TimeSpan.FromSeconds(CityDiscoverySourceTimeoutSeconds));
            try
            {
                await FindOpenStreetMapBuildingAsync(latitude, longitude, city, stateCode,
                    new HashSet<string>(), osmTimeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            if (OpenStreetMapCache.TryGetValue($"{CoordinateKey(latitude, longitude)}|{city}|{stateCode}", out var buildings))
                candidates.AddRange(buildings);
        }
        return candidates.GroupBy(item => CoordinateKey(item.Latitude, item.Longitude), StringComparer.Ordinal)
            .Select(group => group.First()).OrderBy(item => item.DistanceMeters).ToArray();

        static int DistinctCandidateCount(IEnumerable<NearbyBuildingCoordinate> items) =>
            items.Select(item => CoordinateKey(item.Latitude, item.Longitude))
                .Distinct(StringComparer.Ordinal).Count();
    }

    private async Task AddGeoapifyPlaceCandidatesAsync(
        List<NearbyBuildingCoordinate> candidates,
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey)) return;

        foreach (var anchor in CityDiscoveryAnchors)
        {
            foreach (var category in CityPlaceCategories)
            {
                var anchorLatitude = latitude + anchor.LatitudeOffset;
                var anchorLongitude = longitude + anchor.LongitudeOffset;
                var path = "v2/places?categories=" + Uri.EscapeDataString(category) + "&filter=circle:" +
                    anchorLongitude.ToString(CultureInfo.InvariantCulture) + "," +
                    anchorLatitude.ToString(CultureInfo.InvariantCulture) + "," +
                    options.Value.MaximumSimulationDistanceMeters.ToString(CultureInfo.InvariantCulture) +
                    "&bias=proximity:" + anchorLongitude.ToString(CultureInfo.InvariantCulture) + "," +
                    anchorLatitude.ToString(CultureInfo.InvariantCulture) +
                    "&limit=500&apiKey=" + Uri.EscapeDataString(options.Value.ApiKey);
                try
                {
                    await requestGate.WaitAsync(cancellationToken);
                    using var response = await SendWithTransportRetryAsync(
                        new Uri("https://api.geoapify.com/" + path, UriKind.Absolute), cancellationToken);
                    if (!response.IsSuccessStatusCode) continue;
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                    if (!document.RootElement.TryGetProperty("features", out var features) ||
                        features.ValueKind != JsonValueKind.Array) continue;

                    foreach (var feature in features.EnumerateArray())
                    {
                        if (!feature.TryGetProperty("properties", out var properties) ||
                            properties.ValueKind != JsonValueKind.Object ||
                            !properties.TryGetProperty("lat", out var latitudeValue) ||
                            !properties.TryGetProperty("lon", out var longitudeValue) ||
                            !latitudeValue.TryGetDecimal(out var candidateLatitude) ||
                            !longitudeValue.TryGetDecimal(out var candidateLongitude) ||
                            candidateLatitude is < -90 or > 90 || candidateLongitude is < -180 or > 180)
                            continue;

                        var returnedCity = ReadString(properties, "city");
                        var returnedState = ReadString(properties, "state_code");
                        var countryCode = ReadString(properties, "country_code");
                        var formatted = ReadString(properties, "formatted");
                        var street = ReadString(properties, "street");
                        var number = ReadString(properties, "housenumber");
                        var name = ReadString(properties, "name");
                        if (!SameText(returnedCity, city) ||
                            !string.Equals(returnedState, stateCode, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(countryCode, "br", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(formatted) ||
                            (string.IsNullOrWhiteSpace(street) && string.IsNullOrWhiteSpace(number) &&
                             string.IsNullOrWhiteSpace(name)))
                            continue;

                        var distance = DistanceMeters(latitude, longitude, candidateLatitude, candidateLongitude);
                        if (distance > options.Value.MaximumSimulationDistanceMeters) continue;
                        var key = CoordinateKey(candidateLatitude, candidateLongitude);
                        var placeId = ReadString(properties, "place_id") ??
                            $"geoapify:place:{key}";
                        candidates.Add(new NearbyBuildingCoordinate(candidateLatitude, candidateLongitude,
                            placeId, formatted, distance, CityAddressSource));
                    }
                    if (candidates.Select(item => CoordinateKey(item.Latitude, item.Longitude)).Distinct().Count() >=
                        MinimumCityCandidatePoolSize) return;
                }
                catch (HttpRequestException) { }
                catch (JsonException) { }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task AddGeoapifySearchCandidatesAsync(
        List<NearbyBuildingCoordinate> candidates,
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey)) return;
        try
        {
            await requestGate.WaitAsync(cancellationToken);
            var path = "v1/geocode/search?text=" + Uri.EscapeDataString($"{city}, {stateCode}, Brasil") +
                "&filter=countrycode:br&limit=50&format=json&apiKey=" +
                Uri.EscapeDataString(options.Value.ApiKey);
            using var response = await SendWithTransportRetryAsync(new Uri(path, UriKind.Relative), cancellationToken);
            if (!response.IsSuccessStatusCode) return;
            var payload = await response.Content.ReadFromJsonAsync<GeoapifyReverseResponse>(cancellationToken);
            foreach (var result in payload?.Results ?? [])
            {
                var resultState = result.StateCode ?? result.CountyCode;
                if (result.Latitude is null || result.Longitude is null ||
                    string.IsNullOrWhiteSpace(result.PlaceId) || string.IsNullOrWhiteSpace(result.Formatted) ||
                    !IsAddressResultType(result.ResultType) || !MatchesMunicipality(result, city) ||
                    !string.Equals(resultState, stateCode, StringComparison.OrdinalIgnoreCase))
                    continue;
                var distance = DistanceMeters(latitude, longitude, result.Latitude.Value, result.Longitude.Value);
                if (distance > options.Value.MaximumSimulationDistanceMeters) continue;
                candidates.Add(new NearbyBuildingCoordinate(result.Latitude.Value, result.Longitude.Value,
                    result.PlaceId, result.Formatted, distance, CityAddressSource));
            }
        }
        catch (HttpRequestException) { }
        catch (JsonException) { }
        catch (OperationCanceledException) { }
    }

    private static string? ReadString(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public async Task<NearbyBuildingCoordinate?> FindOpenStreetMapBuildingAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        IReadOnlySet<string> excludedCoordinateKeys,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{CoordinateKey(latitude, longitude)}|{city}|{stateCode}";
        if (OpenStreetMapCache.TryGetValue(cacheKey, out var cached))
            return cached.FirstOrDefault(candidate => !excludedCoordinateKeys.Contains(
                CoordinateKey(candidate.Latitude, candidate.Longitude)));
        var candidates = new List<NearbyBuildingCoordinate>();
        foreach (var selector in OpenStreetMapQueries)
        {
            var query = $"[out:json][timeout:20];{selector}(around:10000,{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)});out center tags 1000;";
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["data"] = query
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://overpass-api.de/api/interpreter")
            {
                Content = content
            };
            request.Headers.UserAgent.ParseAdd("InovaSkill-Grespan/1.0 (coordinate-validation)");
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var parsed = ParseOpenStreetMapCandidates(document, latitude, longitude, city, stateCode);
                candidates.AddRange(parsed);
            }
            catch (HttpRequestException) { }
            catch (JsonException) { }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        }
        var ordered = candidates
            .GroupBy(candidate => CoordinateKey(candidate.Latitude, candidate.Longitude), StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.DistanceMeters)
            .ToArray();
        OpenStreetMapCache[cacheKey] = ordered;
        return ordered.FirstOrDefault(candidate => !excludedCoordinateKeys.Contains(
            CoordinateKey(candidate.Latitude, candidate.Longitude)));
    }

    private static IReadOnlyList<NearbyBuildingCoordinate> ParseOpenStreetMapCandidates(
        JsonDocument document,
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode)
    {
        var candidates = new List<NearbyBuildingCoordinate>();
        foreach (var element in document.RootElement.GetProperty("elements").EnumerateArray())
        {
            var point = element.TryGetProperty("center", out var center)
                ? center : element;
            if (!point.TryGetProperty("lat", out var latValue) || !point.TryGetProperty("lon", out var lonValue) ||
                !latValue.TryGetDecimal(out var candidateLatitude) || !lonValue.TryGetDecimal(out var candidateLongitude))
                continue;
            var key = CoordinateKey(candidateLatitude, candidateLongitude);
            var distance = DistanceMeters(latitude, longitude, candidateLatitude, candidateLongitude);
            if (distance > MaximumDistanceMeters) continue;
            var tags = element.TryGetProperty("tags", out var tagValue) ? tagValue : default;
            var street = tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty("addr:street", out var streetValue)
                ? streetValue.GetString() : null;
            var number = tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty("addr:housenumber", out var numberValue)
                ? numberValue.GetString() : null;
            var name = tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty("name", out var nameValue)
                ? nameValue.GetString() : null;
            var displayName = string.Join(", ", new[] { street, number, name, city, stateCode }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(displayName) ||
                (string.IsNullOrWhiteSpace(street) && string.IsNullOrWhiteSpace(number) &&
                 string.IsNullOrWhiteSpace(name)))
                continue;
            var id = element.TryGetProperty("id", out var idValue) ? idValue.ToString() : key;
            var candidate = new NearbyBuildingCoordinate(candidateLatitude, candidateLongitude,
                $"osm:building:{id}", displayName, distance, "OPENSTREETMAP_CITY_ADDRESS");
            candidates.Add(candidate);
        }
        return candidates;
    }

    public async Task<NearbyBuildingCoordinate?> FindAlternateCityAddressAsync(
        decimal latitude,
        decimal longitude,
        string city,
        string stateCode,
        IReadOnlySet<string> excludedCoordinateKeys,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "https://photon.komoot.io/api/?q=" + Uri.EscapeDataString($"{city}, {stateCode}, Brazil") +
            "&limit=100", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        NearbyBuildingCoordinate? best = null;
        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            if (!feature.TryGetProperty("geometry", out var geometry) ||
                !geometry.TryGetProperty("coordinates", out var coordinates) || coordinates.GetArrayLength() < 2)
                continue;
            var candidateLongitude = coordinates[0].GetDecimal();
            var candidateLatitude = coordinates[1].GetDecimal();
            var properties = feature.TryGetProperty("properties", out var value) ? value : default;
            var returnedCity = properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty("city", out var cityValue)
                ? cityValue.GetString() : null;
            var returnedState = properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty("state", out var stateValue)
                ? stateValue.GetString() : null;
            if (!SameText(returnedCity, city) || string.IsNullOrWhiteSpace(returnedState)) continue;
            var key = CoordinateKey(candidateLatitude, candidateLongitude);
            if (excludedCoordinateKeys.Contains(key)) continue;
            var distance = DistanceMeters(latitude, longitude, candidateLatitude, candidateLongitude);
            if (distance > options.Value.MaximumSimulationDistanceMeters) continue;
            var street = properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty("street", out var streetValue)
                ? streetValue.GetString() : null;
            var number = properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty("housenumber", out var numberValue)
                ? numberValue.GetString() : null;
            var displayName = string.Join(", ", new[] { street, number, city, stateCode }
                .Where(item => !string.IsNullOrWhiteSpace(item)));
            var candidate = new NearbyBuildingCoordinate(candidateLatitude, candidateLongitude,
                $"photon:{key}", string.IsNullOrWhiteSpace(displayName) ? $"Endereço em {city}/{stateCode}" : displayName,
                distance, "PHOTON_SIMULATED_NEARBY");
            if (best is null || candidate.DistanceMeters < best.DistanceMeters) best = candidate;
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

    private async Task<GeoapifyReverseResponse> ReverseAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken)
    {
        var path = "v1/geocode/reverse?lat=" + latitude.ToString(CultureInfo.InvariantCulture) +
                   "&lon=" + longitude.ToString(CultureInfo.InvariantCulture) +
                   "&format=json&lang=pt&limit=5&apiKey=" + Uri.EscapeDataString(options.Value.ApiKey);
        var rateLimitRetries = Math.Max(0, options.Value.RateLimitMaximumRetries);
        for (var retry = 0; ; retry++)
        {
            await requestGate.WaitAsync(cancellationToken);
            using var response = await SendWithTransportRetryAsync(new Uri(path, UriKind.Relative), cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && retry < rateLimitRetries)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ??
                    (response.Headers.RetryAfter?.Date is { } retryAt ? retryAt - DateTimeOffset.UtcNow : null);
                var fallbackSeconds = Math.Min(
                    Math.Max(1, options.Value.RateLimitMaximumDelaySeconds),
                    Math.Max(1, options.Value.RateLimitFallbackDelaySeconds) * Math.Pow(2, retry));
                var delay = retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero
                    ? retryAfter.Value
                    : TimeSpan.FromSeconds(fallbackSeconds);
                await Task.Delay(delay, cancellationToken);
                continue;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GeoapifyReverseResponse>(cancellationToken)
                   ?? new GeoapifyReverseResponse([]);
        }
    }

    private async Task<HttpResponseMessage> SendWithTransportRetryAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        var maximumRetries = Math.Max(0, options.Value.TransportMaximumRetries);
        var retryDelay = TimeSpan.FromMilliseconds(Math.Max(0,
            options.Value.TransportRetryDelayMilliseconds));
        for (var retry = 0; ; retry++)
        {
            try
            {
                return await httpClient.GetAsync(uri, cancellationToken);
            }
            catch (HttpRequestException) when (retry < maximumRetries)
            {
                if (retryDelay > TimeSpan.Zero)
                    await Task.Delay(retryDelay * (retry + 1), cancellationToken);
            }
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

    private static bool SameText(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        if (normalizedLeft == normalizedRight) return true;

        // Geoapify currently returns some official Brazilian municipality names
        // with the phonetically equivalent X/CH spelling (for example Echaporã/Exaporã).
        return normalizedLeft.Replace("CH", "X", StringComparison.Ordinal) ==
               normalizedRight.Replace("CH", "X", StringComparison.Ordinal);
    }

    private static bool MatchesMunicipality(GeoapifyReverseResult result, string city) =>
        new[] { result.City, result.Municipality, result.District, result.County }
            .Any(value => SameText(value, city));

    private static bool IsAddressResultType(string? resultType) =>
        !string.IsNullOrWhiteSpace(resultType) &&
        new[] { "building", "residential", "house", "address", "street", "amenity", "commercial", "industrial", "office", "shop" }
            .Contains(resultType, StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant();
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private sealed record GeoapifyReverseResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<GeoapifyReverseResult>? Results);

    private sealed record GeoapifyReverseResult(
        [property: JsonPropertyName("place_id")] string? PlaceId,
        [property: JsonPropertyName("lat")] decimal? Latitude,
        [property: JsonPropertyName("lon")] decimal? Longitude,
        [property: JsonPropertyName("formatted")] string? Formatted,
        [property: JsonPropertyName("housenumber")] string? HouseNumber,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("municipality")] string? Municipality,
        [property: JsonPropertyName("district")] string? District,
        [property: JsonPropertyName("county")] string? County,
        [property: JsonPropertyName("state_code")] string? StateCode,
        [property: JsonPropertyName("county_code")] string? CountyCode,
        [property: JsonPropertyName("country_code")] string? CountryCode,
        [property: JsonPropertyName("result_type")] string? ResultType);
}
