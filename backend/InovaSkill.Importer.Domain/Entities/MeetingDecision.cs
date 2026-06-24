namespace InovaSkill.Importer.Domain.Entities;

public sealed class MeetingDecision
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public long ProblemId { get; set; }
    public string ProblemDescription { get; set; } = string.Empty;
    public string ChosenSolution { get; set; } = string.Empty;
    public string SolutionOrigin { get; set; } = SolutionOriginValue.ManagerAndAi;
    public string Justification { get; set; } = string.Empty;
    public long ResponsibleUserId { get; set; }
    public string ResponsibleName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public int DeadlineDays { get; set; }
    public string Priority { get; set; } = DecisionPriority.Medium;
    public string TrackingMetric { get; set; } = string.Empty;
    public string AcceptedRisk { get; set; } = string.Empty;
    public string NextSteps { get; set; } = string.Empty;
    public string ClosedPendencies { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Meeting Meeting { get; set; } = null!;
    public MeetingProblem Problem { get; set; } = null!;
}

public static class SolutionOriginValue
{
    public const string Manager = "gestor";
    public const string Ai = "ia";
    public const string ManagerAndAi = "gestor_mais_ia";
    public const string Director = "diretor";
    public const string Consensus = "consenso_da_reuniao";
}

public static class DecisionPriority
{
    public const string Low = "baixa";
    public const string Medium = "media";
    public const string High = "alta";
    public const string Critical = "critica";
}
