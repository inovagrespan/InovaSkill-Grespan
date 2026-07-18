namespace InovaSkill.Importer.Domain.Entities;

public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ChatSessionId { get; set; }
    public ChatSession? ChatSession { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
