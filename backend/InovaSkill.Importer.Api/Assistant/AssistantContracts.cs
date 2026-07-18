namespace InovaSkill.Importer.Api.Assistant;

public sealed record AssistantQuestionRequest(string Question);

public sealed record AssistantSource(string Label, string Value);

public sealed record AssistantAnswerResponse(
    string Answer,
    IReadOnlyList<AssistantSource> Sources,
    IReadOnlyList<string> Suggestions,
    string Mode);
