namespace InovaSkill.Importer.Domain.Entities;

public sealed class MeetingQuestion
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public long ProblemId { get; set; }
    public string Question { get; set; } = string.Empty;
    public long ResponsibleUserId { get; set; }
    public string ResponsibleName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Status { get; set; } = QuestionStatus.Pending;
    public DateTime? AnswerDeadline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Meeting Meeting { get; set; } = null!;
    public MeetingProblem Problem { get; set; } = null!;
    public MeetingAnswer? Answer { get; set; }
}

public static class QuestionStatus
{
    public const string Pending = "pendente";
    public const string Answered = "respondida";
    public const string Expired = "vencida";
    public const string Cancelled = "cancelada";
}
