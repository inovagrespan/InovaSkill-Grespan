using System.Net;
using System.Text;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class GeoapifyNearbyBuildingCoordinateProviderTests
{
    [Fact]
    public async Task FindAsync_AcceptsNearestCompatibleNumberedBuilding()
    {
        var handler = new SequenceHandler(Ok(Response(
            Result(-22.2104m, -49.9404m, "20", "Marília", "SP", "building", "far"),
            Result(-22.2101m, -49.9401m, "10", "Marília", "SP", "building", "near"))));

        var result = await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default);

        Assert.NotNull(result);
        Assert.Equal("near", result.PlaceId);
        Assert.Equal(GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource, result.Source);
        Assert.InRange(result.DistanceMeters, 0m, 20m);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task FindAsync_AcceptsNumberedResidentialAddress()
    {
        var handler = new SequenceHandler(Ok(Response(
            Result(-22.2101m, -49.9401m, "42", "Marília", "SP", "residential", "residential-42"))));

        var result = await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default);

        Assert.NotNull(result);
        Assert.Equal("residential-42", result.PlaceId);
    }

    [Fact]
    public async Task FindAsync_AcceptsNumberedStreetAddress()
    {
        var handler = new SequenceHandler(Ok(Response(
            Result(-22.2101m, -49.9401m, "42", "Marília", "SP", "street", "street-42"))));

        var result = await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default);

        Assert.NotNull(result);
        Assert.Equal("street-42", result.PlaceId);
    }

    [Fact]
    public async Task FindAsync_AcceptsResidentialAddressWithoutHouseNumber()
    {
        var handler = new SequenceHandler(Ok(Response(
            Result(-22.2101m, -49.9401m, "", "Marília", "SP", "residential", "residential-no-number"))));

        var result = await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default);

        Assert.NotNull(result);
        Assert.Equal("residential-no-number", result.PlaceId);
    }

    [Theory]
    [InlineData("Bauru", "SP", "building", "10")]
    [InlineData("Marília", "PR", "building", "10")]
    [InlineData("Marília", "SP", "city", "10")]
    [InlineData("Marília", "SP", "city", "")]
    public async Task FindAsync_RejectsIncompatibleOrNonBuildingResult(
        string city,
        string state,
        string resultType,
        string houseNumber)
    {
        var handler = new SequenceHandler(Enumerable.Range(0, 9).Select(_ => Ok(Response(
            Result(-22.2101m, -49.9401m, houseNumber, city, state, resultType, "place")))).ToArray());

        var result = await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default);

        Assert.Null(result);
        Assert.Equal(9, handler.Requests);
    }

    [Fact]
    public async Task FindAsync_RejectsBuildingBeyondMaximumDistance()
    {
        var handler = new SequenceHandler(Enumerable.Range(0, 9).Select(_ => Ok(Response(
            Result(-22.50m, -49.94m, "10", "Marília", "SP", "building", "place")))).ToArray());

        Assert.Null(await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default));
    }

    [Fact]
    public async Task FindAsync_RetriesRateLimitResponse()
    {
        var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
        var handler = new SequenceHandler(limited, Ok(Response(
            Result(-22.2101m, -49.9401m, "10", "Marília", "SP", "building", "place"))));

        var result = await Create(handler).FindAsync(-22.21m, -49.94m, "Marília", "SP", default);

        Assert.NotNull(result);
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task FindAsync_AcceptsDistrictBuildingWhenCountyMatchesExpectedMunicipality()
    {
        var json = "{\"results\":[{\"place_id\":\"place\",\"lat\":-21.2538,\"lon\":-47.8234," +
            "\"formatted\":\"Bonfim Paulista 110\",\"housenumber\":\"110\",\"city\":\"Bonfim Paulista\"," +
            "\"county\":\"Ribeirão Preto\",\"state_code\":\"SP\",\"country_code\":\"br\"," +
            "\"result_type\":\"building\"}]}";

        var result = await Create(new SequenceHandler(Ok(json)))
            .FindAsync(-21.253767m, -47.823365m, "Ribeirão Preto", "SP", default);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task FindAsync_AcceptsGeoapifyPhoneticMunicipalitySpelling()
    {
        var handler = new SequenceHandler(Ok(Response(
            Result(-22.432505m, -50.203945m, "60", "Exaporã", "SP", "building", "place"))));

        var result = await Create(handler)
            .FindAsync(-22.432600m, -50.203800m, "Echaporã", "SP", default);

        Assert.NotNull(result);
        Assert.InRange(result.DistanceMeters, 0m, 25m);
    }

    [Fact]
    public async Task FindAsync_RequiresApiKey()
    {
        var provider = Create(new SequenceHandler(), apiKey: string.Empty);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.FindAsync(-22.21m, -49.94m, "Marília", "SP", default));

        Assert.Contains("GEOAPIFY_API_KEY", exception.Message);
    }

    [Fact]
    public async Task DiscoverCityAddresses_UsesGeoapifyPlacesWithRealCityAddress()
    {
        var handler = new PathAwareHandler(
            places: """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{
              "name":"Mercado Real","country":"Brazil","country_code":"br","state_code":"SP",
              "city":"Marília","street":"Rua Real","housenumber":"123",
              "formatted":"Mercado Real, Rua Real 123, Marília - SP, Brazil",
              "lat":-22.2101,"lon":-49.9401}}]}
            """,
            photon: "{\"features\":[]}",
            overpass: "{\"elements\":[]}");

        var result = await Create(handler).DiscoverCityAddressesAsync(
            -22.21m, -49.94m, "Marília", "SP", CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal("GEOAPIFY_CITY_ADDRESS", candidate.Source);
        Assert.Contains("Rua Real 123", candidate.DisplayName);
        Assert.Contains("v2/places", string.Join('\n', handler.Paths));
        Assert.Contains("categories=building.residential", string.Join('\n', handler.Paths));
    }

    private static GeoapifyNearbyBuildingCoordinateProvider Create(
        HttpMessageHandler handler,
        string apiKey = "test")
    {
        var options = Options.Create(new GeoapifyOptions
        {
            ApiKey = apiKey,
            BaseUrl = "https://example.test/",
            MinimumRequestIntervalMilliseconds = 0,
            TransportRetryDelayMilliseconds = 0,
            RateLimitFallbackDelaySeconds = 1
        });
        return new GeoapifyNearbyBuildingCoordinateProvider(
            new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) },
            options,
            new ImmediateGate());
    }

    private static string Response(params string[] results) =>
        $"{{\"results\":[{string.Join(',', results)}]}}";

    private static string Result(
        decimal latitude,
        decimal longitude,
        string houseNumber,
        string city,
        string state,
        string resultType,
        string placeId) => $$"""
        {"place_id":"{{placeId}}","lat":{{latitude}},"lon":{{longitude}},
        "formatted":"Rua Real {{houseNumber}}, {{city}}", "housenumber":"{{houseNumber}}",
        "city":"{{city}}","county_code":"{{state}}","country_code":"br",
        "result_type":"{{resultType}}"}
        """;

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class ImmediateGate : IGeoapifyRequestGate
    {
        public Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage[] responses;
        private int index;
        public int Requests { get; private set; }

        public SequenceHandler(params HttpResponseMessage[] responses) => this.responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            if (responses.Length == 0) throw new InvalidOperationException("No response configured.");
            var response = responses[Math.Min(index, responses.Length - 1)];
            index++;
            return Task.FromResult(response);
        }
    }

    private sealed class PathAwareHandler : HttpMessageHandler
    {
        private readonly string places;
        private readonly string photon;
        private readonly string overpass;
        public List<string> Paths { get; } = [];

        public PathAwareHandler(string places, string photon, string overpass)
        {
            this.places = places;
            this.photon = photon;
            this.overpass = overpass;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri?.ToString() ?? string.Empty);
            if (request.RequestUri?.ToString().Contains("v2/places", StringComparison.Ordinal) == true)
                return Task.FromResult(Ok(places));
            if (request.RequestUri?.ToString().Contains("photon", StringComparison.Ordinal) == true)
                return Task.FromResult(Ok(photon));
            return Task.FromResult(Ok(overpass));
        }
    }
}
