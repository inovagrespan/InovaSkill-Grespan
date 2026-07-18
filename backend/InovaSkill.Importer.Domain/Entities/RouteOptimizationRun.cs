using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class RouteOptimizationRun
{
    public Guid Id { get; set; }
    public RouteOptimizationScope Scope { get; set; }
    public Guid? TargetRouteId { get; set; }
    public Route? TargetRoute { get; set; }
    public DateOnly ReferenceDate { get; set; }
    public long RequestedByUserId { get; set; }
    public RouteOptimizationRequestedFrom RequestedFrom { get; set; }
    public RouteOptimizationStatus Status { get; set; }
    public int Priority { get; set; }
    public string AlgorithmVersion { get; set; } = string.Empty;
    public string RulesVersion { get; set; } = string.Empty;
    public string? InputHash { get; set; }
    public RouteOptimizationConfidence Confidence { get; set; } = RouteOptimizationConfidence.Insufficient;
    public RouteOptimizationStatus ProgressStage { get; set; } = RouteOptimizationStatus.Pending;
    public decimal? ProgressPercentage { get; set; }
    public Guid? SnapshotImportId { get; set; }
    public RouteImport? SnapshotImport { get; set; }
    public long? SnapshotImportVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public ICollection<RouteOptimizationScenario> Scenarios { get; set; } = [];
}
