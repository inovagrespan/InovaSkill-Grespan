using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class JobExecution
{
    public Guid Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public JobExecutionStatus Status { get; set; }
    public Guid RelatedEntityId { get; set; }
    public RouteImport? Import { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
