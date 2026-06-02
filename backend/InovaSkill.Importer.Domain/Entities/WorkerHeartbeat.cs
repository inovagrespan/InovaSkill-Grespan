namespace InovaSkill.Importer.Domain.Entities;

public sealed class WorkerHeartbeat
{
    public string WorkerId { get; set; } = string.Empty;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public int ProcessedJobsToday { get; set; }
    public DateTime? IdleSinceAt { get; set; }
    public long? CurrentJobId { get; set; }
    public string CurrentTask { get; set; } = string.Empty;
}
