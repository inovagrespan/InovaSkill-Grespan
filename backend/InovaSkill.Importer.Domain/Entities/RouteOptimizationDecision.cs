namespace InovaSkill.Importer.Domain.Entities;

public sealed class RouteOptimizationDecision
{
    public Guid JobExecutionId { get; set; }
    public JobExecution JobExecution { get; set; } = null!;
    public string Status { get; set; } = RouteOptimizationDecisionStatuses.Pending;
    public long? DecidedByUserId { get; set; }
    public AppUser? DecidedByUser { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Justification { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class RouteOptimizationDecisionStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}
