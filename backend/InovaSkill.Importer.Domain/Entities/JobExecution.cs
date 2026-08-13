using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class JobExecution
{
    public Guid Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public int ContractVersion { get; set; } = 1;
    public string Queue { get; set; } = "default";
    public JobExecutionTrigger Trigger { get; set; } = JobExecutionTrigger.System;
    public string ParametersJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public JobExecutionStatus Status { get; set; }
    public Guid RelatedEntityId { get; set; }
    public RouteImport? Import { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal ProgressPercent { get; set; }
    public string? ProgressMessage { get; set; }
    public DateTime? CancellationRequestedAt { get; set; }
    public long? RequestedByUserId { get; set; }
    public Guid? ScheduleId { get; set; }
    public JobSchedule? Schedule { get; set; }
    public Guid? RetriedFromJobExecutionId { get; set; }
    public JobExecution? RetriedFromJobExecution { get; set; }
}
