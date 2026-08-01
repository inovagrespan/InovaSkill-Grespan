using InovaSkill.Importer.Api.Assistant;

namespace InovaSkill.Importer.Tests.Api;

public sealed class OpenAiChatModelClientTests
{
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
}
