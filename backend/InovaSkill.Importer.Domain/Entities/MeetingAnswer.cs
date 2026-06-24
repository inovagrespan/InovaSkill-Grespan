namespace InovaSkill.Importer.Domain.Entities;

public sealed class MeetingAnswer
{
    public long Id { get; set; }
    public long QuestionId { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public MeetingQuestion Question { get; set; } = null!;
}
