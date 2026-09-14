using System.Net;
using System.Text;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class OpenRouteServiceTableClientTests
{
    [Fact]
    public async Task GetTableAsync_SplitsDaysAboveProviderWaypointLimitAndReassemblesMatrix()
    {
        var handler = new BlockMatrixHandler();
        var client = new OpenRouteServiceTableClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://ors.local/") },
            Options.Create(new OpenRouteServiceOptions { ApiKey = "test-key", MaximumWaypoints = 4 }));
        var points = Enumerable.Range(0, 5).Select(index => new OsrmMatrixPoint(
            Guid.NewGuid(), index == 0 ? OsrmMatrixPointTypes.Depot : OsrmMatrixPointTypes.Municipality,
            -22m + index, -49m + index)).ToArray();

        var result = await client.GetTableAsync(new OsrmTableRequest("MONDAY", points), default);

        Assert.Equal(9, handler.RequestCount);
        Assert.Equal(5, result.DurationsSeconds.Count);
        Assert.All(result.DurationsSeconds, row => Assert.Equal(5, row.Count));
    }

    [Fact]
    public async Task GetTableAsync_UsesLongitudeLatitudeAndReturnsRoadMetrics()
    {
        var handler = new RecordingHandler("""
            {"durations":[[0,120],[115,0]],"distances":[[0,4500],[4400,0]]}
            """);
        var client = new OpenRouteServiceTableClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://ors.local/") },
            Options.Create(new OpenRouteServiceOptions { ApiKey = "test-key", MaximumWaypoints = 50 }));
        var points = new[]
        {
            new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, -22.21m, -49.95m),
            new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Municipality, -22.20m, -49.94m)
        };

        var result = await client.GetTableAsync(new OsrmTableRequest("MONDAY", points), default);

        Assert.Equal("OPENROUTESERVICE_MATRIX_DRIVING_CAR", result.Source);
        Assert.Equal(120m, result.DurationsSeconds[0][1]);
        Assert.Equal(4_400m, result.DistancesMeters[1][0]);
        Assert.Equal("test-key", handler.Authorization);
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal(-49.95m, json.RootElement.GetProperty("locations")[0][0].GetDecimal());
        Assert.Equal(-22.21m, json.RootElement.GetProperty("locations")[0][1].GetDecimal());
        Assert.Equal("0", json.RootElement.GetProperty("sources")[0].GetString());
        Assert.Equal("0", json.RootElement.GetProperty("destinations")[0].GetString());
    }

    [Fact]
    public async Task GetTableAsync_PreservesSafeProviderReasonOnHttpFailure()
    {
        var client = new OpenRouteServiceTableClient(
            new HttpClient(new StaticFailureHandler()) { BaseAddress = new Uri("https://ors.local/") },
            Options.Create(new OpenRouteServiceOptions { ApiKey = "secret", MaximumWaypoints = 50 }));

        var error = await Assert.ThrowsAsync<OsrmTableException>(() => client.GetTableAsync(
            new OsrmTableRequest("MONDAY",
            [
                new(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, -22.21m, -49.95m),
                new(Guid.NewGuid(), OsrmMatrixPointTypes.Municipality, -22.20m, -49.94m)
            ]), CancellationToken.None));

        Assert.Contains("HTTP 403", error.Message);
        Assert.Contains("Daily quota exceeded", error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    private sealed class RecordingHandler(string response) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class BlockMatrixHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var rows = body.RootElement.GetProperty("sources").GetArrayLength();
            var columns = body.RootElement.GetProperty("destinations").GetArrayLength();
            var matrix = Enumerable.Range(0, rows)
                .Select(_ => Enumerable.Repeat<decimal?>(1m, columns).ToArray()).ToArray();
            var payload = JsonSerializer.Serialize(new { durations = matrix, distances = matrix });
            return new(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class StaticFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"error":{"message":"Daily quota exceeded"}}""", Encoding.UTF8, "application/json")
            });
    }
}
