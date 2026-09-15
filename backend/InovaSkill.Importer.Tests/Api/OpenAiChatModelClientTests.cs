using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

namespace InovaSkill.Importer.Tests.Api;

public sealed class OpenAiChatModelClientTests
{
    [Fact]
    public async Task SendAsync_SendsConfiguredMaximumOutputTokens()
    {
        var handler = new CapturingHandler();
        var options = Options.Create(new AssistantOptions
        {
            OpenAiApiKey = "test-key",
            MaximumOutputTokens = 8192
        });
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"openai-client-{Guid.NewGuid()}").Options);
        var client = new OpenAiChatModelClient(
            new StaticHttpClientFactory(new HttpClient(handler)),
            options,
            NullLogger<OpenAiChatModelClient>.Instance,
            new AiConsumptionService(db, options));

        await client.SendAsync(new ChatModelRequest(
            "gpt-5.4", "Responda com os dados disponíveis.",
            [new ChatModelInputMessage("user", "Analise as rotas.")], []), default);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(8192, request.RootElement.GetProperty("max_output_tokens").GetInt32());
    }

    [Fact]
    public void ReadUsage_ReturnsInputAndOutputTokens()
    {
        using var document = System.Text.Json.JsonDocument.Parse("""{"usage":{"input_tokens":123,"output_tokens":45}}""");

        var usage = OpenAiChatModelClient.ReadUsage(document.RootElement);

        Assert.Equal(123, usage.InputTokens);
        Assert.Equal(45, usage.OutputTokens);
    }

    [Fact]
    public void ReadUsage_UsesZeroWhenUsageIsAbsent()
    {
        using var document = System.Text.Json.JsonDocument.Parse("{}");
        Assert.Equal((0, 0), OpenAiChatModelClient.ReadUsage(document.RootElement));
    }
    [Fact]
    public void ReadProviderError_ExtractsSafeValidationDetails()
    {
        const string body = """
            {"error":{"message":"Schema inválido.","type":"invalid_request_error","param":"tools[2].parameters","code":"invalid_function_parameters"}}
            """;

        var error = OpenAiChatModelClient.ReadProviderError(body);

        Assert.Equal("invalid_function_parameters", error.Code);
        Assert.Equal("tools[2].parameters", error.Param);
        Assert.Equal("Schema inválido.", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não é json")]
    [InlineData("{}")]
    public void ReadProviderError_WhenBodyHasNoExpectedError_ReturnsControlledDetails(string body)
    {
        Assert.Equal(OpenAiProviderError.Unknown, OpenAiChatModelClient.ReadProviderError(body));
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":\"response-test\",\"output_text\":\"Resposta completa.\",\"output\":[],\"usage\":{}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
