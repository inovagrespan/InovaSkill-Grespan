namespace InovaSkill.Importer.Domain.Entities;

public sealed class WhatsAppUserLink
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public AppUser? User { get; set; }
    public string NormalizedPhone { get; set; } = string.Empty;
    public string Status { get; set; } = WhatsAppUserLinkStatuses.Pending;
    public string? VerificationCodeHash { get; set; }
    public DateTime? VerificationExpiresAt { get; set; }
    public int VerificationAttempts { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FloodBlockedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<ChatSession> ChatSessions { get; set; } = [];
}

public static class WhatsAppUserLinkStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Revoked = "revoked";
}
