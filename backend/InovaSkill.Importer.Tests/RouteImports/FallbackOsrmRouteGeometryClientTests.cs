using System.Net;
using System.Text;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class FallbackOsrmRouteGeometryClientTests
{
    [Fact]
    public async Task GetRouteAsync_UsesOsrmWhenOpenRouteServiceReturnsRateLimit()
    {
        var primary = new OpenRouteServiceRouteGeometryClient(
            Client(new StaticHandler(HttpStatusCode.TooManyRequests, "{}"), "https://ors.test/"),
            Options.Create(new OpenRouteServiceOptions { ApiKey = "test-key", MaximumWaypoints = 50 }));
        var fallback = new OsrmRouteClient(
            Client(new StaticHandler(HttpStatusCode.OK,
                """{"code":"Ok","routes":[{"distance":1200,"duration":180,"geometry":{"coordinates":[[-49.95,-22.21],[-49.94,-22.20]]}}]}"""), "https://osrm.test/"),
            Options.Create(new OsrmOptions { RouteMaximumWaypoints = 50 }));
        var client = new FallbackOsrmRouteGeometryClient(primary, fallback,
            NullLogger<FallbackOsrmRouteGeometryClient>.Instance);

        var result = await client.GetRouteAsync(Points(), CancellationToken.None);

        Assert.Equal("OSRM_ROUTE_DRIVING", result.Source);
        Assert.Equal(1200m, result.DistanceMeters);
        Assert.Equal(180m, result.DurationSeconds);
    }

    private static IReadOnlyList<RouteGeometryPoint> Points() =>
    [
        new(Guid.NewGuid(), "Grespan Matriz", -22.21m, -49.95m),
        new(Guid.NewGuid(), "Cliente", -22.20m, -49.94m),
        new(Guid.NewGuid(), "Grespan Matriz", -22.21m, -49.95m),
    ];

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
