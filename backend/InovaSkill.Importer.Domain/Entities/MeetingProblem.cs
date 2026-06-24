namespace InovaSkill.Importer.Domain.Entities;

public sealed class MeetingProblem
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public string Sector { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = ProblemSeverity.Medium;
    public string Origin { get; set; } = ProblemOrigin.Discussion;
    public long CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public bool ApprovedByDirector { get; set; }
    public string AiSuggestion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Meeting Meeting { get; set; } = null!;
    public List<MeetingQuestion> Questions { get; set; } = [];
}

public static class ProblemSeverity
{
    public const string Low = "baixa";
    public const string Medium = "media";
    public const string High = "alta";
    public const string Critical = "critica";
}

public static class ProblemOrigin
{
    public const string Discussion = "discussao_atual";
    public const string CriticalPending = "pendencia_critica";
    public const string OverdueAction = "acao_atrasada";
    public const string PreviousMeeting = "reuniao_anterior";
    public const string AiSuggestion = "sugestao_ia";
    public const string Director = "diretor";
}
