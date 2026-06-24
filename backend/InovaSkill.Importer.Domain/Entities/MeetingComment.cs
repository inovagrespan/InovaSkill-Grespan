namespace InovaSkill.Importer.Domain.Entities;

public sealed class MeetingComment
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public bool IsImportant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Meeting Meeting { get; set; } = null!;
}
