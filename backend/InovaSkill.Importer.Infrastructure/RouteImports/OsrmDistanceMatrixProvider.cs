using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OsrmDistanceMatrixProvider(
    HttpClient httpClient,
    IOptions<RouteOptimizationOptions> options) : IDistanceMatrixProvider, IRouteTravelMatrixProvider
{
    private const int SecondsPerMinute = 60;
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

    public async Task<RouteTravelMatrix> GetMatrixAsync(
        IReadOnlyList<GeoPoint> points,
        CancellationToken cancellationToken)
    {
        if (points.Count == 0)
        {
            return new RouteTravelMatrix([], [], Method);
        }

        if (points.Count > routeOptimizationOptions.MaximumMatrixPoints)
        {
            throw new InvalidOperationException(
                $"A matriz OSRM aceita no máximo {routeOptimizationOptions.MaximumMatrixPoints} pontos por cálculo.");
        }

        var coordinates = string.Join(';', points.Select(point =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{point.Longitude},{point.Latitude}")));
        var path = $"/table/v1/driving/{coordinates}?annotations=duration,distance";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(routeOptimizationOptions.OsrmTimeoutSeconds));

        var response = await httpClient.GetAsync(path, timeout.Token);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OsrmTableResponse>(timeout.Token)
            ?? throw new InvalidOperationException("Resposta vazia do OSRM Table.");
        if (!string.Equals(payload.Code, "Ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"OSRM Table retornou o código {payload.Code}.");
        }

        var distances = ConvertMatrix(
            payload.Distances,
            points.Count,
            value => Math.Round(value / 1000m, 2, MidpointRounding.AwayFromZero),
            "distância");
        var durations = ConvertMatrix(
            payload.Durations,
            points.Count,
            value => (int)Math.Round(value / SecondsPerMinute, MidpointRounding.AwayFromZero),
            "duração");
        return new RouteTravelMatrix(distances, durations, "OsrmTable");
    }

    private static IReadOnlyList<IReadOnlyList<T>> ConvertMatrix<T>(
        IReadOnlyList<IReadOnlyList<decimal?>>? source,
        int expectedSize,
        Func<decimal, T> convert,
        string matrixName)
    {
        if (source is null || source.Count != expectedSize || source.Any(row => row.Count != expectedSize))
        {
            throw new InvalidOperationException($"OSRM Table retornou matriz de {matrixName} com dimensão inválida.");
        }

        return source.Select(row => (IReadOnlyList<T>)row.Select(value =>
            value.HasValue
                ? convert(value.Value)
                : throw new InvalidOperationException($"OSRM Table não encontrou {matrixName} entre duas paradas.")).ToArray()).ToArray();
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

    private sealed record OsrmTableResponse(
        string Code,
        IReadOnlyList<IReadOnlyList<decimal?>>? Distances,
        IReadOnlyList<IReadOnlyList<decimal?>>? Durations);
}
