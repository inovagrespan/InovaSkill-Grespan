using System.Text.Json;
using InovaSkill.Importer.Api.Assistant;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AssistantContractsTests
{
    [Fact]
    public void AssistantQuestionRequest_accepts_frontend_lowercase_message_payload()
    {
        var request = JsonSerializer.Deserialize<AssistantQuestionRequest>(
            "{\"sessionId\":null,\"message\":\"teste\"}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal("teste", request!.Message);
        Assert.Null(request.Question);
    }
}
