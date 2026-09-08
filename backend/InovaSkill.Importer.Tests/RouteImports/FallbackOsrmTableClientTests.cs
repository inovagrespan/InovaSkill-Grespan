using System.Net;
using System.Text;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class FallbackOsrmTableClientTests
{
    [Fact]
    public async Task GetTableAsync_UsesOsrmWhenOpenRouteServiceRefusesRequest()
    {
        var primary = new OpenRouteServiceTableClient(
            Client(new StaticHandler(HttpStatusCode.Forbidden,
                """{"error":{"message":"Daily quota exceeded"}}"""), "https://ors.test/"),
            Options.Create(new OpenRouteServiceOptions { ApiKey = "test-key", MaximumWaypoints = 50 }));
        var fallback = new OsrmTableClient(
            Client(new StaticHandler(HttpStatusCode.OK,
                """{"code":"Ok","durations":[[0,10],[11,0]],"distances":[[0,100],[110,0]]}"""), "https://osrm.test/"),
            Options.Create(new OsrmOptions { MatrixBlockSize = 50, MaximumParallelRequests = 1 }));
        var client = new FallbackOsrmTableClient(primary, fallback, NullLogger<FallbackOsrmTableClient>.Instance);

        var result = await client.GetTableAsync(Request(), CancellationToken.None);

        Assert.Equal("OSRM_TABLE_DRIVING", result.Source);
        Assert.Equal(100m, result.DistancesMeters[0][1]);
        Assert.Equal(11m, result.DurationsSeconds[1][0]);
    }

    [Fact]
    public async Task GetTableAsync_ReportsBothFailures()
    {
        var primary = new OpenRouteServiceTableClient(
            Client(new StaticHandler(HttpStatusCode.Forbidden, """{"error":{"message":"Quota exceeded"}}"""), "https://ors.test/"),
            Options.Create(new OpenRouteServiceOptions { ApiKey = "test-key", MaximumWaypoints = 50 }));
        var fallback = new OsrmTableClient(
            Client(new StaticHandler(HttpStatusCode.ServiceUnavailable, "{}"), "https://osrm.test/"),
            Options.Create(new OsrmOptions { MatrixBlockSize = 50, MaximumParallelRequests = 1 }));
        var client = new FallbackOsrmTableClient(primary, fallback, NullLogger<FallbackOsrmTableClient>.Instance);

        var error = await Assert.ThrowsAsync<OsrmTableException>(() =>
            client.GetTableAsync(Request(), CancellationToken.None));

        Assert.Contains("HTTP 403", error.Message);
        Assert.Contains("fallback OSRM também falhou", error.Message);
    }

    private static OsrmTableRequest Request() => new("MONDAY",
    [
        new(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, -22.21m, -49.95m),
        new(Guid.NewGuid(), OsrmMatrixPointTypes.Municipality, -22.20m, -49.94m)
    ]);

    private static HttpClient Client(HttpMessageHandler handler, string baseUrl) =>
        new(handler) { BaseAddress = new Uri(baseUrl) };

    private sealed class StaticHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}
