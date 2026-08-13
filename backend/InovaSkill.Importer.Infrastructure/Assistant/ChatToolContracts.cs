namespace InovaSkill.Importer.Api.Assistant;

public interface IChatTool
{
    string Name { get; }
    string Description { get; }
    object GetParameterSchema();

    Task<ChatToolResult> ExecuteAsync(
        string argumentsJson,
        ChatExecutionContext context,
        CancellationToken cancellationToken);
}

public sealed record ChatExecutionContext(long UserId, string Role);

public sealed record ChatToolResult(bool Success, object Payload, int RecordCount, string? ErrorMessage = null)
{
    public static ChatToolResult Ok(object payload, int recordCount) => new(true, payload, recordCount);

    public static ChatToolResult Fail(string message) => new(false, new { error = message }, 0, message);
}
