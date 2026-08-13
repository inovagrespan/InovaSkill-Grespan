namespace InovaSkill.Importer.Domain.Entities;

public sealed class JobSchedule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public int ContractVersion { get; set; } = 1;
    public string ParametersJson { get; set; } = "{}";
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
    public bool IsActive { get; set; } = true;
    public long CreatedByUserId { get; set; }
    public long UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? NextExecutionAt { get; set; }
    public ICollection<JobExecution> Executions { get; set; } = [];
}
