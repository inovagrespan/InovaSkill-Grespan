namespace InovaSkill.Importer.Domain.Entities;

public sealed class KnowledgeMemory
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = KnowledgeMemoryScopes.Company;
    public long? OwnerUserId { get; set; }
    public AppUser? OwnerUser { get; set; }
    public long CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
    public Guid SourceChatMessageId { get; set; }
    public ChatMessage SourceChatMessage { get; set; } = null!;
    public Guid? SupersedesMemoryId { get; set; }
    public KnowledgeMemory? SupersedesMemory { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string EmbeddingJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class KnowledgeMemoryScopes
{
    public const string Company = "company";
    public const string User = "user";
}
