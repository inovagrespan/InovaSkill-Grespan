using System.Net;
using System.Text;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class OpenRouteServiceRouteGeometryClientTests
{
    [Fact]
    public async Task GetRouteAsync_SendsHereCoordinatesAsLongitudeLatitudeAndReturnsGeoJsonGeometry()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {"features":[{"geometry":{"coordinates":[[-49.95,-22.21],[-49.94,-22.20]]},"properties":{"summary":{"distance":1234.5,"duration":321}}}]}
            """);

        var result = await Client(handler).GetRouteAsync(Points(2), CancellationToken.None);

        Assert.Equal("test-key", handler.Authorization);
        Assert.Equal("/v2/directions/driving-car/geojson", handler.Path);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal(-49.95m, request.RootElement.GetProperty("coordinates")[0][0].GetDecimal());
        Assert.Equal(-22.21m, request.RootElement.GetProperty("coordinates")[0][1].GetDecimal());
        Assert.Equal("OPENROUTESERVICE_DIRECTIONS_DRIVING_CAR", result.Source);
        Assert.Equal(1234.5m, result.DistanceMeters);
        Assert.Equal(-49.94m, result.Geometry[1][0]);
    }

    [Fact]
    public async Task GetRouteAsync_WhenProviderCannotRoute_IdentifiesFailedSegment()
    {
        var exception = await Assert.ThrowsAsync<RouteGeometryException>(() =>
            Client(new RecordingHandler(HttpStatusCode.BadRequest, "{}"))
                .GetRouteAsync(Points(2), CancellationToken.None));

        Assert.Contains("Ponto 0", exception.Message);
        Assert.Contains("Ponto 1", exception.Message);
        Assert.Contains("HTTP 400", exception.Message);
    }

    [Fact]
    public async Task GetRouteAsync_SplitsRoutesAtConfiguredWaypointLimit()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {"features":[{"geometry":{"coordinates":[[-49.95,-22.21],[-49.94,-22.20]]},"properties":{"summary":{"distance":100,"duration":10}}}]}
            """);

        var result = await Client(handler, maximumWaypoints: 3).GetRouteAsync(Points(5), CancellationToken.None);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(200m, result.DistanceMeters);
        Assert.Equal(3, result.Geometry.Count);
    }

    private static IReadOnlyList<RouteGeometryPoint> Points(int count) => Enumerable.Range(0, count)
        .Select(index => new RouteGeometryPoint(
            Guid.NewGuid(), $"Ponto {index}", -22.21m + index * 0.01m, -49.95m + index * 0.01m))
        .ToArray();

    private static OpenRouteServiceRouteGeometryClient Client(
        HttpMessageHandler handler,
        int maximumWaypoints = 50) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://ors.local/") },
        Options.Create(new OpenRouteServiceOptions
        {
            ApiKey = "test-key",
            MaximumWaypoints = maximumWaypoints
        }));

    private sealed class RecordingHandler(HttpStatusCode statusCode, string response) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? Path { get; private set; }
        public string? Body { get; private set; }
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Authorization = request.Headers.GetValues("Authorization").Single();
            Path = request.RequestUri!.AbsolutePath;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
