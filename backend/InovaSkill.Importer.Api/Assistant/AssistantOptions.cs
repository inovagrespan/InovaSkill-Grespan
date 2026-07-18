namespace InovaSkill.Importer.Api.Assistant;

public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5.6-luna";
    public int MaximumQuestionLength { get; set; } = 800;
}
