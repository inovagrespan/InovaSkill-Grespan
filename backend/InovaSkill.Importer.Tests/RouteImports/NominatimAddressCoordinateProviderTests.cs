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
        var provider = Provider(handler);

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
        var provider = Provider(handler);
        var result = await provider.FindAsync(new("RUA", "A", "10", null, "Marília", "SP", null), default);
        Assert.Equal("NOT_FOUND", result.Status);
        Assert.Null(result.Latitude);
        Assert.StartsWith(AddressCoordinateFailureReasons.IncompatibleMunicipalityOrState, result.FailureReason);
    }

    [Fact]
    public async Task FindAsync_AcceptsStreetApproximationWhenHouseNumberIsNotConfirmed()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            [{"place_id":123,"lat":"-22","lon":"-49","address":{"city":"Cândido Rodrigues","ISO3166-2-lvl4":"BR-SP"}}]
            """);
        var provider = Provider(handler);
        var result = await provider.FindAsync(new("AVENIDA", "SAULLE BORGHI", "296", "CENTRO", "CANDIDO RODRIGUES", "SP", "15930000"), default);
        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal(AddressCoordinateMatchLevels.Street, result.MatchLevel);
        Assert.Contains("aproximada", result.FailureReason);
    }

    [Fact]
    public async Task FindAsync_FallsBackToPostalCodeWhenStreetIsMissing()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"cep":"17510520","state":"SP","city":"Marília","neighborhood":"Palmital","street":"Rua A",
             "location":{"coordinates":{"longitude":"-49.94","latitude":"-22.21"}}}
            """);
        var provider = Provider(handler);

        var result = await provider.FindAsync(new(null, "", null, "Palmital", "Marília", "SP", "17510520"), default);

        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal(AddressCoordinateMatchLevels.PostalCode, result.MatchLevel);
        Assert.Equal("BRASIL_API_POSTAL_CODE", result.Source);
        Assert.Contains("cep/v2/17510520", handler.Uri!.AbsoluteUri);
    }

    [Fact]
    public async Task FindAsync_UsesPostalCodeBeforeNominatimWhenHouseNumberIsMissing()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"cep":"17510520","state":"SP","city":"Marília","neighborhood":"Palmital","street":"Rua A",
             "location":{"coordinates":{"longitude":"-49.94","latitude":"-22.21"}}}
            """);
        var provider = Provider(handler);

        var result = await provider.FindAsync(
            new("RUA", "A", null, "Palmital", "Marília", "SP", "17510520"), default);

        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal(AddressCoordinateMatchLevels.PostalCode, result.MatchLevel);
        Assert.Equal("BRASIL_API_POSTAL_CODE", result.Source);
        Assert.Contains("brasilapi.test/api/cep/v2/17510520", handler.Uri!.AbsoluteUri);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task FindAsync_ExposesRetryAfterOnRateLimit()
    {
        var handler = new Handler(HttpStatusCode.TooManyRequests, "[]", TimeSpan.FromMilliseconds(1));
        var provider = Provider(handler);
        var exception = await Assert.ThrowsAsync<NominatimRateLimitException>(() =>
            provider.FindAsync(new("RUA", "A", "10", null, "Marília", "SP", null), default));
        Assert.Equal(TimeSpan.FromMilliseconds(1), exception.RetryAfter);
    }

    [Fact]
    public async Task FindAsync_UsesMunicipalityAsLastAuditableFallback()
    {
        var handler = new SequenceHandler(
            "[]",
            "[]",
            """
            [{"place_id":456,"lat":"-22.2171","lon":"-49.9501","display_name":"Marília, SP",
              "address":{"city":"Marília","ISO3166-2-lvl4":"BR-SP"}}]
            """);
        var provider = Provider(handler);

        var result = await provider.FindAsync(new("RUA", "INEXISTENTE", "10", null, "Marília", "SP", null), default);

        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal(AddressCoordinateMatchLevels.Municipality, result.MatchLevel);
        Assert.Equal("Coordenada aproximada pelo município.", result.FailureReason);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task FindAsync_RejectsMissingMunicipalityWithoutCallingProvider()
    {
        var handler = new Handler(HttpStatusCode.OK, "[]");
        var result = await Provider(handler).FindAsync(new("RUA", "A", "10", null, "", "SP", null), default);

        Assert.Equal("NOT_FOUND", result.Status);
        Assert.StartsWith(AddressCoordinateFailureReasons.InsufficientData, result.FailureReason);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task FindAsync_PropagatesProviderTimeoutAsTechnicalFailure()
    {
        var provider = Provider(new ThrowingHandler(new TaskCanceledException("timeout")));
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            provider.FindAsync(new("RUA", "A", "10", null, "Marília", "SP", null), default));
    }

    [Fact]
    public async Task FindAsync_RetriesTransientSslTransportFailure()
    {
        var handler = new TransientTransportFailureHandler();

        var result = await Provider(handler).FindAsync(
            new("RUA", "A", "10", null, "Marília", "SP", null), default);

        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class ImmediateGate : INominatimRequestGate { public Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private static NominatimAddressCoordinateProvider Provider(Handler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") },
        new ImmediateGate(),
        Options.Create(new BrasilApiOptions { BaseUrl = "https://brasilapi.test/api/" }),
        Options.Create(new NominatimOptions { TransportRetryDelayMilliseconds = 0 }));
    private static NominatimAddressCoordinateProvider Provider(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") },
        new ImmediateGate(),
        Options.Create(new BrasilApiOptions { BaseUrl = "https://brasilapi.test/api/" }),
        Options.Create(new NominatimOptions { TransportRetryDelayMilliseconds = 0 }));
    private sealed class Handler(HttpStatusCode status, string content, TimeSpan? retryAfter = null) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Uri = request.RequestUri;
            var response = new HttpResponseMessage(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
            if (retryAfter.HasValue) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
            return Task.FromResult(response);
        }
    }
    private sealed class SequenceHandler(params string[] contents) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = contents[Math.Min(RequestCount, contents.Length - 1)];
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }
    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
    private sealed class TransientTransportFailureHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 1)
                return Task.FromException<HttpResponseMessage>(new HttpRequestException(
                    "The SSL connection could not be established.",
                    new System.Security.Authentication.AuthenticationException("TLS handshake interrupted.")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    [{"place_id":123,"lat":"-22.217100","lon":"-49.950100","display_name":"Rua A, Marília",
                      "address":{"city":"Marília","house_number":"10","ISO3166-2-lvl4":"BR-SP"}}]
                    """, Encoding.UTF8, "application/json")
            });
        }
    }
}
