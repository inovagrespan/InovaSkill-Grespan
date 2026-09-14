using System.Net;
using System.Text;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class GeoapifyAddressCoordinateProviderTests
{
    [Fact]
    public async Task FindAsync_SendsBrazilFilterAndMapsCompatibleExactResult()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"results":[{"place_id":"51-test","lat":-22.2171,"lon":-49.9501,
              "formatted":"Rua A, 10, Marília, SP","housenumber":"10","city":"Marília","state_code":"SP"}]}
            """);

        var result = await Provider(handler).FindAsync(
            new("RUA", "A", "10", "Centro", "MARILIA", "SP", "17500000"), default);

        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal(AddressCoordinateMatchLevels.Exact, result.MatchLevel);
        Assert.Equal("GEOAPIFY_EXACT", result.Source);
        Assert.Equal(-22.2171m, result.Latitude);
        Assert.Contains("v1/geocode/search", handler.Uri!.AbsolutePath);
        Assert.Contains("filter=countrycode:br", handler.Uri.Query);
        Assert.Contains("format=json", handler.Uri.Query);
        Assert.Contains("apiKey=test-key", handler.Uri.Query);
    }

    [Fact]
    public async Task FindAsync_RejectsResultFromDifferentMunicipality()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"results":[{"place_id":"51-test","lat":-22,"lon":-49,
              "formatted":"Rua A, Bauru, SP","housenumber":"10","city":"Bauru","state_code":"SP"}]}
            """);

        var result = await Provider(handler).FindAsync(
            new("RUA", "A", "10", null, "Marília", "SP", null), default);

        Assert.Equal("NOT_FOUND", result.Status);
        Assert.StartsWith(AddressCoordinateFailureReasons.IncompatibleMunicipalityOrState, result.FailureReason);
    }

    [Fact]
    public async Task FindAsync_RequiresConfiguredApiKeyBeforeCallingProvider()
    {
        var handler = new Handler(HttpStatusCode.OK, "{\"results\":[]}");
        var provider = Provider(handler, string.Empty);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.FindAsync(
            new("RUA", "A", "10", null, "Marília", "SP", null), default));

        Assert.Contains("Geoapify:ApiKey", exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task FindAsync_ExposesRateLimitAsTechnicalFailure()
    {
        var handler = new Handler(HttpStatusCode.TooManyRequests, "{\"results\":[]}", TimeSpan.FromSeconds(2));

        var exception = await Assert.ThrowsAsync<GeoapifyRateLimitException>(() => Provider(handler).FindAsync(
            new("RUA", "A", "10", null, "Marília", "SP", null), default));

        Assert.Equal(TimeSpan.FromSeconds(2), exception.RetryAfter);
    }

    private static GeoapifyAddressCoordinateProvider Provider(Handler handler, string apiKey = "test-key") => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.geoapify.test/") },
        Options.Create(new BrasilApiOptions { BaseUrl = "https://brasilapi.test/api/" }),
        Options.Create(new GeoapifyOptions
        {
            ApiKey = apiKey,
            TransportMaximumRetries = 0,
            TransportRetryDelayMilliseconds = 0
        }));

    private sealed class Handler(HttpStatusCode status, string content, TimeSpan? retryAfter = null) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Uri = request.RequestUri;
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
            if (retryAfter.HasValue)
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
            return Task.FromResult(response);
        }
    }
}
