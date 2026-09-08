using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OpenRouteServiceRouteGeometryClient(
    HttpClient httpClient,
    IOptions<OpenRouteServiceOptions> options) : IRouteGeometryClient
{
    private const string SourceName = "OPENROUTESERVICE_DIRECTIONS_DRIVING_CAR";
    private readonly OpenRouteServiceOptions settings = Validate(options.Value);

    public async Task<RouteGeometryResult> GetRouteAsync(
        IReadOnlyList<RouteGeometryPoint> points,
        CancellationToken cancellationToken)
    {
        ValidatePoints(points);
        var geometry = new List<IReadOnlyList<decimal>>();
        decimal distanceMeters = 0;
        decimal durationSeconds = 0;

        foreach (var segment in Split(points, settings.MaximumWaypoints))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v2/directions/driving-car/geojson");
            request.Headers.TryAddWithoutValidation("Authorization", settings.ApiKey);
            request.Content = JsonContent.Create(new
            {
                coordinates = segment.Select(point => new[] { point.Longitude, point.Latitude }).ToArray()
            });

            OpenRouteServicePayload payload;
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new RouteGeometryException(
                        $"O openrouteservice não encontrou caminho entre {segment[0].Label} e {segment[^1].Label} (HTTP {(int)response.StatusCode}).");
                payload = await response.Content.ReadFromJsonAsync<OpenRouteServicePayload>(cancellationToken)
                    ?? throw new RouteGeometryException("O openrouteservice retornou um trajeto vazio.");
            }
            catch (RouteGeometryException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                throw new RouteGeometryException("Não foi possível consultar o openrouteservice.", exception);
            }

            var feature = payload.Features?.SingleOrDefault();
            var coordinates = feature?.Geometry?.Coordinates;
            var summary = feature?.Properties?.Summary;
            if (coordinates is null || coordinates.Count < 2 || summary is null ||
                summary.Distance < 0 || summary.Duration < 0)
                throw new RouteGeometryException(
                    $"O openrouteservice retornou um trajeto inválido entre {segment[0].Label} e {segment[^1].Label}.");

            var segmentGeometry = coordinates.Select(ValidateCoordinate).ToArray();
            geometry.AddRange(geometry.Count == 0 ? segmentGeometry : segmentGeometry.Skip(1));
            distanceMeters += summary.Distance;
            durationSeconds += summary.Duration;
        }

        return new RouteGeometryResult(SourceName, points, geometry, distanceMeters, durationSeconds);
    }

    private static void ValidatePoints(IReadOnlyList<RouteGeometryPoint> points)
    {
        if (points.Count < 2)
            throw new ArgumentException("O trajeto rodoviário exige pelo menos dois pontos.", nameof(points));
        if (points.Any(point => point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180))
            throw new ArgumentException("O trajeto contém coordenada inválida.", nameof(points));
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
            throw new RouteGeometryException("O openrouteservice retornou uma geometria com coordenada inválida.");
        return [coordinate[0], coordinate[1]];
    }

    private static OpenRouteServiceOptions Validate(OpenRouteServiceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException("OpenRouteService:ApiKey não foi configurada.");
        if (options.MaximumWaypoints < 2)
            throw new InvalidOperationException("OpenRouteService:MaximumWaypoints deve ser pelo menos 2.");
        return options;
    }

    private sealed record OpenRouteServicePayload(
        [property: JsonPropertyName("features")] IReadOnlyList<OpenRouteServiceFeature>? Features);

    private sealed record OpenRouteServiceFeature(
        [property: JsonPropertyName("geometry")] OpenRouteServiceGeometry? Geometry,
        [property: JsonPropertyName("properties")] OpenRouteServiceProperties? Properties);

    private sealed record OpenRouteServiceGeometry(
        [property: JsonPropertyName("coordinates")] IReadOnlyList<IReadOnlyList<decimal>> Coordinates);

    private sealed record OpenRouteServiceProperties(
        [property: JsonPropertyName("summary")] OpenRouteServiceSummary? Summary);

    private sealed record OpenRouteServiceSummary(
        [property: JsonPropertyName("distance")] decimal Distance,
        [property: JsonPropertyName("duration")] decimal Duration);
}
