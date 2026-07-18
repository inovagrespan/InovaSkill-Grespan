namespace InovaSkill.Importer.Api.Assistant;

public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5.4";
    public int MaximumQuestionLength { get; set; } = 800;
    public int MaximumToolExecutionsPerMessage { get; set; } = 5;
    public int MaximumHistoryMessages { get; set; } = 20;
    public int MaximumGeneralSearchResults { get; set; } = 20;
    public int OpenAiTimeoutSeconds { get; set; } = 30;
    public int ToolTimeoutSeconds { get; set; } = 10;
}
