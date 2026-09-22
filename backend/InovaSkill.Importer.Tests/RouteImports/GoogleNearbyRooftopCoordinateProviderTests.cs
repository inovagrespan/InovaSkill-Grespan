using System.Net;
using System.Text;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class GoogleNearbyRooftopCoordinateProviderTests
{
    [Fact]
    public async Task FindAsync_AcceptsNearestCompatibleRooftopAndCalculatesDistance()
    {
        var handler = new JsonHandler(Response(-22.2101m, -49.9401m, "Marília", "SP", "ROOFTOP"));
        var provider = Create(handler);

        var result = await provider.FindAsync(-22.21m, -49.94m, "Marília", "SP", default);

        Assert.NotNull(result);
        Assert.Equal("place-1", result.PlaceId);
        Assert.InRange(result.DistanceMeters, 0m, 20m);
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData("Bauru", "SP", "ROOFTOP")]
    [InlineData("Marília", "PR", "ROOFTOP")]
    [InlineData("Marília", "SP", "APPROXIMATE")]
    public async Task FindAsync_RejectsIncompatibleOrNonRooftopResult(
        string city, string state, string granularity)
    {
        var handler = new JsonHandler(Response(-22.2101m, -49.9401m, city, state, granularity));

        var result = await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default);

        Assert.Null(result);
        Assert.Equal(9, handler.Requests);
    }

    [Fact]
    public async Task FindAsync_RejectsRooftopBeyondMaximumDistance()
    {
        var handler = new JsonHandler(Response(-22.30m, -49.94m, "Marília", "SP", "ROOFTOP"));
        Assert.Null(await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default));
    }

    [Fact]
    public async Task FindAsync_IgnoresIncompleteProviderResults()
    {
        var handler = new JsonHandler("{\"results\":[{}]}");
        Assert.Null(await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default));
        Assert.Equal(9, handler.Requests);
    }

    private static GoogleNearbyRooftopCoordinateProvider Create(HttpMessageHandler handler)
    {
        var options = Options.Create(new GoogleGeocodingOptions
        {
            ApiKey = "test", BaseUrl = "https://example.test/", MinimumRequestIntervalMilliseconds = 1
        });
        return new GoogleNearbyRooftopCoordinateProvider(
            new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
            new GoogleGeocodingRequestGate(options));
    }

    private static string Response(decimal latitude, decimal longitude, string city, string state, string granularity) => $$"""
        {"results":[{"placeId":"place-1","location":{"latitude":{{latitude}},"longitude":{{longitude}}},
        "granularity":"{{granularity}}","formattedAddress":"Rua Real, 10, {{city}} - {{state}}",
        "addressComponents":[{"longText":"{{city}}","shortText":"{{city}}","types":["locality"]},
        {"longText":"São Paulo","shortText":"{{state}}","types":["administrative_area_level_1"]}],
        "types":["street_address"]}]}
        """;

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
