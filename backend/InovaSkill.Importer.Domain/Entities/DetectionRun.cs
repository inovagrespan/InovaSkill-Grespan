using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class DetectionRun
{
    public Guid Id { get; set; }
    public Guid DetectorDefinitionId { get; set; }
    public DetectorDefinition? DetectorDefinition { get; set; }
    public DetectionRunStatus Status { get; set; } = DetectionRunStatus.Queued;
    public DetectionTrigger Trigger { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int AttemptCount { get; set; }
    public int AnalyzedItems { get; set; }
    public int FindingsCount { get; set; }
    public string? StatusReason { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public Guid? RetryOfRunId { get; set; }
}
