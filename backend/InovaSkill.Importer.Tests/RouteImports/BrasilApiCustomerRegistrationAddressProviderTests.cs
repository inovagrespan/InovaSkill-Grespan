using System.Net;
using System.Text;
using System.Net.Http.Headers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class BrasilApiCustomerRegistrationAddressProviderTests
{
    [Fact]
    public async Task FindByCnpjAsync_MapsRegistrationAddressFromBrasilApi()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {"cep":"17500-000","uf":"SP","municipio":"MARILIA","logradouro":"AVENIDA BRASIL",
             "numero":"100","complemento":"SALA 2","bairro":"CENTRO"}
            """);
        var provider = CreateProvider(handler);

        var result = await provider.FindByCnpjAsync("07.050.702/0002-00", CancellationToken.None);

        Assert.Equal(CustomerRegistrationAddressStatuses.Resolved, result.Status);
        Assert.Equal("17500-000", result.PostalCode);
        Assert.Equal("SP", result.StateCode);
        Assert.Equal("MARILIA", result.City);
        Assert.Equal("AVENIDA BRASIL", result.Street);
        Assert.Equal("100", result.Number);
        Assert.Equal("SALA 2", result.Complement);
        Assert.Equal("CENTRO", result.Neighborhood);
        Assert.Equal("/api/cnpj/v1/07050702000200", handler.RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, CustomerRegistrationAddressStatuses.InvalidDocument)]
    [InlineData(HttpStatusCode.NotFound, CustomerRegistrationAddressStatuses.NotFound)]
    public async Task FindByCnpjAsync_MapsExpectedFunctionalFailures(
        HttpStatusCode statusCode,
        string expectedStatus)
    {
        var result = await CreateProvider(new StubHandler(statusCode, "{}"))
            .FindByCnpjAsync("07050702000200", CancellationToken.None);

        Assert.Equal(expectedStatus, result.Status);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task FindByCnpjAsync_RejectsMalformedDocumentWithoutHttpRequest()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");

        var result = await CreateProvider(handler).FindByCnpjAsync("123", CancellationToken.None);

        Assert.Equal(CustomerRegistrationAddressStatuses.InvalidDocument, result.Status);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task FindByCnpjAsync_RetriesRateLimitAndReturnsSuccessfulResult()
    {
        var handler = new StubHandler(
            (HttpStatusCode.TooManyRequests, "{}"),
            (HttpStatusCode.OK, "{\"municipio\":\"MARILIA\",\"uf\":\"SP\"}"));

        var result = await CreateProvider(handler, maximumRetries: 1)
            .FindByCnpjAsync("07050702000200", CancellationToken.None);

        Assert.Equal(CustomerRegistrationAddressStatuses.Resolved, result.Status);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task FindByCnpjAsync_ThrowsSpecificExceptionAfterRateLimitRetries()
    {
        var handler = new StubHandler(
            (HttpStatusCode.TooManyRequests, "{}"),
            (HttpStatusCode.TooManyRequests, "{}"));

        await Assert.ThrowsAsync<BrasilApiRateLimitException>(() =>
            CreateProvider(handler, maximumRetries: 1)
                .FindByCnpjAsync("07050702000200", CancellationToken.None));
        Assert.Equal(2, handler.RequestCount);
    }

    private static BrasilApiCustomerRegistrationAddressProvider CreateProvider(
        StubHandler handler, int maximumRetries = 0)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://brasilapi.com.br/api/") };
        return new BrasilApiCustomerRegistrationAddressProvider(client, Options.Create(new BrasilApiOptions
        {
            RequestsPerSecond = int.MaxValue,
            RateLimitMaximumRetries = maximumRetries,
            RateLimitFallbackDelaySeconds = 0,
            RateLimitMaximumDelaySeconds = 0,
            RateLimitJitterMaximumMilliseconds = 0
        }));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Content)> responses;
        public Uri? RequestUri { get; private set; }
        public int RequestCount { get; private set; }

        public StubHandler(HttpStatusCode statusCode, string content)
            : this((statusCode, content))
        {
        }

        public StubHandler(params (HttpStatusCode StatusCode, string Content)[] responses)
        {
            this.responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestCount++;
            var responseDefinition = responses.Count > 1 ? responses.Dequeue() : responses.Peek();
            var response = new HttpResponseMessage(responseDefinition.StatusCode)
            {
                Content = new StringContent(responseDefinition.Content, Encoding.UTF8, "application/json")
            };
            if (responseDefinition.StatusCode == HttpStatusCode.TooManyRequests)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        }
    }
}
