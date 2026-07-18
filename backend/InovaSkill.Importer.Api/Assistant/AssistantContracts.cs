namespace InovaSkill.Importer.Api.Assistant;

public sealed record AssistantQuestionRequest(Guid? SessionId, string? Message, string? Question);

public sealed record AssistantSource(string Label, string Value);

public sealed record AssistantAnswerResponse(
    Guid SessionId,
    string Answer,
    IReadOnlyList<AssistantSource> Sources,
    IReadOnlyList<string> Suggestions,
    string Mode);
