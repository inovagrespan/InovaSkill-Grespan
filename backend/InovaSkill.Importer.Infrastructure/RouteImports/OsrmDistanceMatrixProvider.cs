using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OsrmDistanceMatrixProvider(
    HttpClient httpClient,
    IOptions<RouteOptimizationOptions> options) : IDistanceMatrixProvider
{
    private readonly RouteOptimizationOptions routeOptimizationOptions = options.Value;
    private readonly ConcurrentDictionary<string, Task<decimal>> distanceCache = new();

    public string Method => "OsrmRoadDistance";

    public Task<decimal> GetDistanceKmAsync(
        GeoPoint origin,
        GeoPoint destination,
        CancellationToken cancellationToken)
    {
        if (origin == destination)
        {
            return Task.FromResult(0m);
        }

        var cacheKey = CacheKey(origin, destination);
        return distanceCache.GetOrAdd(cacheKey, _ => FetchDistanceKmAsync(origin, destination, cancellationToken));
    }

    private async Task<decimal> FetchDistanceKmAsync(
        GeoPoint origin,
        GeoPoint destination,
        CancellationToken cancellationToken)
    {
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"/route/v1/driving/{origin.Longitude},{origin.Latitude};{destination.Longitude},{destination.Latitude}?overview=false&alternatives=false&steps=false");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(routeOptimizationOptions.OsrmTimeoutSeconds));

        var response = await httpClient.GetAsync(path, timeout.Token);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OsrmRouteResponse>(timeout.Token)
            ?? throw new InvalidOperationException("Resposta vazia do OSRM.");
        var route = payload.Routes.FirstOrDefault()
            ?? throw new InvalidOperationException("OSRM não retornou rota entre os pontos informados.");

        return Math.Round(route.Distance / 1000m, 2, MidpointRounding.AwayFromZero);
    }

    private static string CacheKey(GeoPoint origin, GeoPoint destination)
    {
        var first = PointKey(origin);
        var second = PointKey(destination);
        return string.CompareOrdinal(first, second) <= 0 ? $"{first}|{second}" : $"{second}|{first}";
    }

    private static string PointKey(GeoPoint point) =>
        string.Create(CultureInfo.InvariantCulture, $"{point.Latitude:0.000000},{point.Longitude:0.000000}");

    public static Uri NormalizeBaseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("RouteOptimization:OsrmBaseUrl deve ser uma URL absoluta.");
        }

        return uri;
    }

    private sealed record OsrmRouteResponse(IReadOnlyList<OsrmRoute> Routes);

    private sealed record OsrmRoute(decimal Distance);
}
