using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OsrmRouteClient(HttpClient httpClient, IOptions<OsrmOptions> options) : IRouteGeometryClient
{
    private const string SourceName = "OSRM_ROUTE_DRIVING";
    private readonly int maximumWaypoints = ValidateMaximumWaypoints(options.Value.RouteMaximumWaypoints);

    public async Task<RouteGeometryResult> GetRouteAsync(
        IReadOnlyList<RouteGeometryPoint> points,
        CancellationToken cancellationToken)
    {
        if (points.Count < 2)
            throw new ArgumentException("O trajeto rodoviário exige pelo menos dois pontos.", nameof(points));
        if (points.Any(point => point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180))
            throw new ArgumentException("O trajeto contém coordenada inválida.", nameof(points));

        var geometry = new List<IReadOnlyList<decimal>>();
        decimal distanceMeters = 0;
        decimal durationSeconds = 0;

        foreach (var segment in Split(points, maximumWaypoints))
        {
            var coordinates = string.Join(';', segment.Select(point =>
                $"{Format(point.Longitude)},{Format(point.Latitude)}"));
            var path = $"route/v1/driving/{coordinates}?overview=full&geometries=geojson&steps=false&continue_straight=false";
            OsrmRoutePayload payload;
            try
            {
                payload = await httpClient.GetFromJsonAsync<OsrmRoutePayload>(path, cancellationToken)
                    ?? throw new RouteGeometryException("O OSRM retornou um trajeto vazio.");
            }
            catch (RouteGeometryException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                throw new RouteGeometryException("Não foi possível calcular o trajeto rodoviário no OSRM.", exception);
            }

            var route = payload.Routes?.SingleOrDefault();
            if (!string.Equals(payload.Code, "Ok", StringComparison.OrdinalIgnoreCase) || route is null)
                throw new RouteGeometryException(
                    $"O OSRM não encontrou caminho entre {segment[0].Label} e {segment[^1].Label}: {payload.Code ?? "código ausente"}.");
            if (route.Geometry?.Coordinates is null || route.Geometry.Coordinates.Count < 2 ||
                route.Distance < 0 || route.Duration < 0)
                throw new RouteGeometryException(
                    $"O OSRM retornou um trajeto inválido entre {segment[0].Label} e {segment[^1].Label}.");

            var segmentGeometry = route.Geometry.Coordinates.Select(ValidateCoordinate).ToArray();
            geometry.AddRange(geometry.Count == 0 ? segmentGeometry : segmentGeometry.Skip(1));
            distanceMeters += route.Distance;
            durationSeconds += route.Duration;
        }

        return new RouteGeometryResult(SourceName, points, geometry, distanceMeters, durationSeconds);
    }

    private static IEnumerable<IReadOnlyList<RouteGeometryPoint>> Split(
        IReadOnlyList<RouteGeometryPoint> points,
        int maximumWaypoints)
    {
        var start = 0;
        while (start < points.Count - 1)
        {
            var count = Math.Min(maximumWaypoints, points.Count - start);
            yield return points.Skip(start).Take(count).ToArray();
            start += count - 1;
        }
    }

    private static IReadOnlyList<decimal> ValidateCoordinate(IReadOnlyList<decimal> coordinate)
    {
        if (coordinate.Count < 2 || coordinate[0] is < -180 or > 180 || coordinate[1] is < -90 or > 90)
            throw new RouteGeometryException("O OSRM retornou uma geometria com coordenada inválida.");
        return [coordinate[0], coordinate[1]];
    }

    private static int ValidateMaximumWaypoints(int value) => value >= 2
        ? value
        : throw new InvalidOperationException("Osrm:RouteMaximumWaypoints deve ser pelo menos 2.");

    private static string Format(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private sealed record OsrmRoutePayload(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("routes")] IReadOnlyList<OsrmRoutePayloadItem>? Routes);

    private sealed record OsrmRoutePayloadItem(
        [property: JsonPropertyName("distance")] decimal Distance,
        [property: JsonPropertyName("duration")] decimal Duration,
        [property: JsonPropertyName("geometry")] OsrmGeoJsonGeometry? Geometry);

    private sealed record OsrmGeoJsonGeometry(
        [property: JsonPropertyName("coordinates")] IReadOnlyList<IReadOnlyList<decimal>> Coordinates);
}
