namespace InovaSkill.Importer.Api.Assistant;

public sealed record AssistantQuestionRequest(Guid? SessionId, string? Message, string? Question);

public sealed record AssistantSource(string Label, string Value);

public sealed record AssistantAnswerResponse(
    Guid SessionId,
    string Answer,
    IReadOnlyList<AssistantSource> Sources,
    IReadOnlyList<string> Suggestions,
    string Mode);

public sealed record AssistantConversationSummaryResponse(
    Guid SessionId,
    string Preview,
    DateTime UpdatedAt);

public sealed record AssistantConversationMessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt);

public sealed record AssistantConversationResponse(
    Guid SessionId,
    IReadOnlyList<AssistantConversationMessageResponse> Messages);
