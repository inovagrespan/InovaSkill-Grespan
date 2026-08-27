using System.Net;
using System.Text;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class NominatimAddressCoordinateProviderTests
{
    [Fact]
    public async Task RequestGate_SerializesStartsWithAtLeastOneSecondBetweenThem()
    {
        var gate = new NominatimRequestGate(Options.Create(new NominatimOptions
        {
            MinimumRequestIntervalMilliseconds = 1000
        }));
        await gate.WaitAsync(default);
        var started = DateTime.UtcNow;
        await gate.WaitAsync(default);
        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(950));
    }

    [Fact]
    public async Task FindAsync_SendsRequiredQueryAndMapsCompatibleResult()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            [{"place_id":123,"lat":"-22.217100","lon":"-49.950100","display_name":"Rua A, Marília",
              "address":{"city":"Marília","house_number":"10","ISO3166-2-lvl4":"BR-SP"}}]
            """);
        var provider = new NominatimAddressCoordinateProvider(new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") }, new ImmediateGate());

        var result = await provider.FindAsync(new("RUA", "A", "10", "Centro", "MARILIA", "SP", "17500000"), default);

        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal(-22.217100m, result.Latitude);
        Assert.Contains("format=json", handler.Uri!.Query);
        Assert.Contains("addressdetails=1", handler.Uri.Query);
        Assert.Contains("countrycodes=br", handler.Uri.Query);
        Assert.Contains("Rua A", Uri.UnescapeDataString(handler.Uri.Query), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("17500-000", Uri.UnescapeDataString(handler.Uri.Query));
    }

    [Fact]
    public async Task FindAsync_RejectsResultFromDifferentMunicipality()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            [{"place_id":123,"lat":"-22","lon":"-49","address":{"city":"Bauru","ISO3166-2-lvl4":"BR-SP"}}]
            """);
        var provider = new NominatimAddressCoordinateProvider(new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") }, new ImmediateGate());
        var result = await provider.FindAsync(new("RUA", "A", "10", null, "Marília", "SP", null), default);
        Assert.Equal("NOT_FOUND", result.Status);
        Assert.Null(result.Latitude);
    }

    [Fact]
    public async Task FindAsync_DoesNotAcceptStreetCentroidWithoutConfirmedHouseNumber()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            [{"place_id":123,"lat":"-22","lon":"-49","address":{"city":"Cândido Rodrigues","ISO3166-2-lvl4":"BR-SP"}}]
            """);
        var provider = new NominatimAddressCoordinateProvider(new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") }, new ImmediateGate());
        var result = await provider.FindAsync(new("AVENIDA", "SAULLE BORGHI", "296", "CENTRO", "CANDIDO RODRIGUES", "SP", "15930000"), default);
        Assert.Equal("NOT_FOUND", result.Status);
        Assert.Contains("número", result.FailureReason);
    }

    [Fact]
    public async Task FindAsync_ExposesRetryAfterOnRateLimit()
    {
        var handler = new Handler(HttpStatusCode.TooManyRequests, "[]", TimeSpan.FromMilliseconds(1));
        var provider = new NominatimAddressCoordinateProvider(new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") }, new ImmediateGate());
        var exception = await Assert.ThrowsAsync<NominatimRateLimitException>(() =>
            provider.FindAsync(new("RUA", "A", "10", null, "Marília", "SP", null), default));
        Assert.Equal(TimeSpan.FromMilliseconds(1), exception.RetryAfter);
    }

    private sealed class ImmediateGate : INominatimRequestGate { public Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class Handler(HttpStatusCode status, string content, TimeSpan? retryAfter = null) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            var response = new HttpResponseMessage(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
            if (retryAfter.HasValue) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
            return Task.FromResult(response);
        }
    }
}
