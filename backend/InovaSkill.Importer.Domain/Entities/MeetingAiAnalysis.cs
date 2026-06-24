namespace InovaSkill.Importer.Domain.Entities;

public sealed class MeetingAiAnalysis
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public long ProblemId { get; set; }
    public string ProblemDescription { get; set; } = string.Empty;
    public string ProposedSolution { get; set; } = string.Empty;
    public bool MakesSense { get; set; }
    public string PositivePoints { get; set; } = string.Empty;
    public string NegativePoints { get; set; } = string.Empty;
    public string Risks { get; set; } = string.Empty;
    public string ExpectedImpact { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string AlternativeSolution { get; set; } = string.Empty;
    public string SuggestedDecision { get; set; } = string.Empty;
    public string RelatedPendencies { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Meeting Meeting { get; set; } = null!;
    public MeetingProblem Problem { get; set; } = null!;
}
