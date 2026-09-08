using System.Net;
using System.Text;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class OsrmRouteClientTests
{
    [Fact]
    public async Task GetRouteAsync_RequestsGeoJsonAndPreservesRoadGeometry()
    {
        var handler = new RecordingHandler(_ => """
            {"code":"Ok","routes":[{"distance":12500.5,"duration":930,"geometry":{"coordinates":[[-49.95,-22.21],[-49.94,-22.20],[-49.90,-22.18]]}}]}
            """);

        var result = await Client(handler, 50).GetRouteAsync(Points(2), CancellationToken.None);

        Assert.Contains("route/v1/driving/-49.95,-22.21;-49.94,-22.2", handler.Paths.Single());
        Assert.Contains("geometries=geojson", handler.Paths.Single());
        Assert.Equal(12500.5m, result.DistanceMeters);
        Assert.Equal(3, result.Geometry.Count);
        Assert.Equal(-49.94m, result.Geometry[1][0]);
        Assert.Equal(-22.20m, result.Geometry[1][1]);
    }

    [Fact]
    public async Task GetRouteAsync_SplitsLargeRoutesAndJoinsGeometryWithoutDuplicatingBoundary()
    {
        var handler = new RecordingHandler(_ => """
            {"code":"Ok","routes":[{"distance":100,"duration":10,"geometry":{"coordinates":[[-49,-22],[-48,-21]]}}]}
            """);

        var result = await Client(handler, 3).GetRouteAsync(Points(5), CancellationToken.None);

        Assert.Equal(2, handler.Paths.Count);
        Assert.Equal(200m, result.DistanceMeters);
        Assert.Equal(3, result.Geometry.Count);
    }

    [Theory]
    [InlineData("{\"code\":\"NoRoute\",\"routes\":[]}")]
    [InlineData("{\"code\":\"Ok\",\"routes\":[{\"distance\":1,\"duration\":1,\"geometry\":{\"coordinates\":[]}}]}")]
    public async Task GetRouteAsync_RejectsMissingRoadGeometry(string json)
    {
        await Assert.ThrowsAsync<RouteGeometryException>(() =>
            Client(new RecordingHandler(_ => json), 50).GetRouteAsync(Points(2), CancellationToken.None));
    }

    private static IReadOnlyList<RouteGeometryPoint> Points(int count) => Enumerable.Range(0, count)
        .Select(index => new RouteGeometryPoint(Guid.NewGuid(), $"Ponto {index}", -22.21m + index * 0.01m, -49.95m + index * 0.01m))
        .ToArray();

    private static OsrmRouteClient Client(HttpMessageHandler handler, int maximumWaypoints) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://osrm.local/") },
        Options.Create(new OsrmOptions { RouteMaximumWaypoints = maximumWaypoints }));

    private sealed class RecordingHandler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(request), Encoding.UTF8, "application/json")
            });
        }
    }
}
