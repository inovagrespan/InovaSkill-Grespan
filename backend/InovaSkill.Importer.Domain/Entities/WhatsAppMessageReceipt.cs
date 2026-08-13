namespace InovaSkill.Importer.Domain.Entities;

public sealed class WhatsAppMessageReceipt
{
    public Guid Id { get; set; }
    public string ProviderMessageId { get; set; } = string.Empty;
    public Guid WhatsAppUserLinkId { get; set; }
    public WhatsAppUserLink? WhatsAppUserLink { get; set; }
    public Guid? ChatSessionId { get; set; }
    public ChatSession? ChatSession { get; set; }
    public Guid? ChatMessageId { get; set; }
    public ChatMessage? ChatMessage { get; set; }
    public string Direction { get; set; } = WhatsAppMessageDirections.Inbound;
    public string MessageType { get; set; } = string.Empty;
    public string Status { get; set; } = WhatsAppMessageStatuses.Received;
    public string? TextContent { get; set; }
    public string? MediaReference { get; set; }
    public string? ProviderOutboundMessageId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class WhatsAppMessageDirections
{
    public const string Inbound = "inbound";
    public const string Outbound = "outbound";
}

public static class WhatsAppMessageStatuses
{
    public const string Received = "received";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
