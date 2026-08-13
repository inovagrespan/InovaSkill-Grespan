namespace InovaSkill.Importer.Domain.Entities;

public sealed class ChatSession
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public AppUser? User { get; set; }
    public string Channel { get; set; } = ChatSessionChannels.Web;
    public Guid? WhatsAppUserLinkId { get; set; }
    public WhatsAppUserLink? WhatsAppUserLink { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = [];
}

public static class ChatSessionChannels
{
    public const string Web = "web";
    public const string WhatsApp = "whatsapp";
}
