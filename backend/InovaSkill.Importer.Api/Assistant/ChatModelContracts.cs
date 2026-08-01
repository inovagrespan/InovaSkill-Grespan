namespace InovaSkill.Importer.Api.Assistant;

public interface IChatModelClient
{
    Task<ChatModelResponse> SendAsync(ChatModelRequest request, CancellationToken cancellationToken);
}

public sealed record ChatModelRequest(
    string Model,
    string Instructions,
    IReadOnlyList<ChatModelInputMessage> Messages,
    IReadOnlyList<ChatToolDefinition> Tools,
    string? PreviousResponseId = null,
    ChatModelRequestPurpose Purpose = ChatModelRequestPurpose.Answer,
    object? TextFormat = null,
    bool EnableWebSearch = false);

public enum ChatModelRequestPurpose { ScopeClassification, Answer, ExternalResearch, MemoryExtraction }

public sealed record ChatModelInputMessage(string Role, string Content);

public sealed record ChatModelResponse(
    string ResponseId,
    string? Text,
    IReadOnlyList<ChatModelToolCall> ToolCalls,
    IReadOnlyList<ChatModelSource>? Sources = null,
    int InputTokens = 0,
    int OutputTokens = 0);

public sealed record ChatModelSource(string Title, string Url);

public sealed record ChatModelToolCall(string CallId, string Name, string ArgumentsJson);

public sealed record ChatToolDefinition(
    string Name,
    string Description,
    object Parameters);
