namespace InovaSkill.Importer.Domain.Entities;

public sealed class ChatSession
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
