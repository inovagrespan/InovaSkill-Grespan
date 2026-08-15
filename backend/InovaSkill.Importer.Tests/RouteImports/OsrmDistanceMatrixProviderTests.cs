using System.Net;
using System.Text;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class OsrmDistanceMatrixProviderTests
{
    [Fact]
    public async Task GetMatrixAsync_UsesTableEndpointAndConvertsMetersAndSeconds()
    {
        var handler = new StubHttpHandler("""
            {"code":"Ok","distances":[[0,12500],[12400,0]],"durations":[[0,900],[840,0]]}
            """);
        var provider = CreateProvider(handler);

        var matrix = await provider.GetMatrixAsync(
            [new GeoPoint(-22.2m, -49.8m), new GeoPoint(-22.3m, -49.9m)],
            CancellationToken.None);

        Assert.Contains("/table/v1/driving/-49.8,-22.2;-49.9,-22.3", handler.RequestUri!.AbsoluteUri);
        Assert.Contains("annotations=duration,distance", handler.RequestUri.Query);
        Assert.Equal(12.5m, matrix.DistancesKm[0][1]);
        Assert.Equal(15, matrix.DurationsMinutes[0][1]);
        Assert.Equal(14, matrix.DurationsMinutes[1][0]);
        Assert.Equal("OsrmTable", matrix.Method);
    }

    [Fact]
    public async Task GetMatrixAsync_RejectsUnreachablePair()
    {
        var provider = CreateProvider(new StubHttpHandler("""
            {"code":"Ok","distances":[[0,null],[null,0]],"durations":[[0,null],[null,0]]}
            """));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetMatrixAsync(
            [new GeoPoint(-22.2m, -49.8m), new GeoPoint(-22.3m, -49.9m)],
            CancellationToken.None));

        Assert.Contains("não encontrou distância", exception.Message);
    }

    [Fact]
    public async Task GetMatrixAsync_EnforcesConfiguredPointLimitBeforeHttpCall()
    {
        var handler = new StubHttpHandler("{}");
        var provider = CreateProvider(handler, maximumMatrixPoints: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetMatrixAsync(
            [new GeoPoint(-22.2m, -49.8m), new GeoPoint(-22.3m, -49.9m)],
            CancellationToken.None));

        Assert.Null(handler.RequestUri);
    }

    private static OsrmDistanceMatrixProvider CreateProvider(StubHttpHandler handler, int maximumMatrixPoints = 100) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://osrm.test") },
            Options.Create(new RouteOptimizationOptions
            {
                OsrmBaseUrl = "http://osrm.test",
                OsrmTimeoutSeconds = 5,
                MaximumMatrixPoints = maximumMatrixPoints
            }));

    private sealed class StubHttpHandler(string json) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
