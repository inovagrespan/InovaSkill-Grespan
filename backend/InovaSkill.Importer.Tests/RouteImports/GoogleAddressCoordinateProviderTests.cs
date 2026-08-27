using System.Net;
using System.Text;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class GoogleAddressCoordinateProviderTests
{
    [Fact]
    public async Task FindAsync_SendsSecretInHeaderAndMapsExactRooftopAddress()
    {
        var handler = new Handler(HttpStatusCode.OK, ExactResponse);
        var provider = CreateProvider(handler);

        var result = await provider.FindAsync(
            new("RUA", "A", "10", "Centro", "MARILIA", "SP", "17500000"), default);

        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal("GOOGLE_GEOCODING", provider.SourceName);
        Assert.Equal(-22.217100m, result.Latitude);
        Assert.Equal("secret", handler.ApiKey);
        Assert.DoesNotContain("secret", handler.Uri!.ToString());
        Assert.Contains("regionCode=BR", handler.Uri.Query);
        Assert.Contains("Rua%20A", handler.Uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("APPROXIMATE", "10", "MARILIA", "SP")]
    [InlineData("ROOFTOP", "11", "MARILIA", "SP")]
    [InlineData("ROOFTOP", "10", "BAURU", "SP")]
    [InlineData("ROOFTOP", "10", "MARILIA", "RJ")]
    public async Task FindAsync_RejectsNonExactOrIncompatibleResult(
        string granularity, string number, string city, string state)
    {
        var response = ExactResponse
            .Replace("ROOFTOP", granularity)
            .Replace("\"longText\":\"10\"", $"\"longText\":\"{number}\"")
            .Replace("\"longText\":\"MARILIA\"", $"\"longText\":\"{city}\"")
            .Replace("\"shortText\":\"SP\"", $"\"shortText\":\"{state}\"");
        var result = await CreateProvider(new Handler(HttpStatusCode.OK, response)).FindAsync(
            new("RUA", "A", "10", null, "MARILIA", "SP", null), default);
        Assert.Equal("NOT_FOUND", result.Status);
        Assert.Null(result.Latitude);
    }

    [Fact]
    public async Task FindAsync_RequiresConfiguredApiKey()
    {
        var provider = new GoogleAddressCoordinateProvider(new HttpClient(new Handler(HttpStatusCode.OK, "{}"))
            { BaseAddress = new Uri("https://geocode.test/v4/") }, Options.Create(new GoogleGeocodingOptions()), new ImmediateGate());
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.FindAsync(new("RUA", "A", "10", null, "MARILIA", "SP", null), default));
        Assert.Contains("GOOGLE_MAPS_API_KEY", exception.Message);
    }

    [Fact]
    public async Task FindAsync_TreatsResponseWithoutResultsAsNotFound()
    {
        var result = await CreateProvider(new Handler(HttpStatusCode.OK, "{}"))
            .FindAsync(new("RUA", "A", "10", null, "MARILIA", "SP", null), default);
        Assert.Equal("NOT_FOUND", result.Status);
        Assert.Null(result.Latitude);
    }

    private static GoogleAddressCoordinateProvider CreateProvider(Handler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://geocode.test/v4/") },
        Options.Create(new GoogleGeocodingOptions { ApiKey = "secret" }),
        new ImmediateGate());

    private const string ExactResponse = """
        {"results":[{"placeId":"place-1","granularity":"ROOFTOP","formattedAddress":"Rua A, 10, Marília - SP",
        "location":{"latitude":-22.217100,"longitude":-49.950100},"types":["street_address"],
        "addressComponents":[
          {"longText":"10","shortText":"10","types":["street_number"]},
          {"longText":"MARILIA","shortText":"MARILIA","types":["locality"]},
          {"longText":"São Paulo","shortText":"SP","types":["administrative_area_level_1"]}
        ]}]}
        """;

    private sealed class Handler(HttpStatusCode status, string content) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public string? ApiKey { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            ApiKey = request.Headers.GetValues("X-Goog-Api-Key").Single();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ImmediateGate : IGoogleGeocodingRequestGate
    {
        public Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
