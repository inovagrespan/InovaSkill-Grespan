namespace InovaSkill.Importer.Domain.Entities;

public sealed class MeetingAction
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public long? DecisionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long ResponsibleUserId { get; set; }
    public string ResponsibleName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public int DeadlineDays { get; set; }
    public string Priority { get; set; } = ActionPriority.Medium;
    public string Status { get; set; } = ActionStatus.Pending;
    public string CompletionEvidence { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? DeadlineAt { get; set; }

    public Meeting Meeting { get; set; } = null!;
    public MeetingDecision? Decision { get; set; }
}

public static class ActionStatus
{
    public const string Pending = "pendente";
    public const string InProgress = "em_andamento";
    public const string Completed = "concluida";
    public const string Overdue = "atrasada";
    public const string Cancelled = "cancelada";
    public const string Escalated = "escalada";
}

public static class ActionPriority
{
    public const string Low = "baixa";
    public const string Medium = "media";
    public const string High = "alta";
    public const string Critical = "critica";
}
