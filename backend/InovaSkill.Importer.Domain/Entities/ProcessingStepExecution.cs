namespace InovaSkill.Importer.Domain.Entities;

public sealed class ProcessingStepExecution
{
    public long Id { get; set; }
    public long FileJobId { get; set; }
    public string Step { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "running";
    public int ProcessedRows { get; set; }
    public int ErrorCount { get; set; }

    public TimeSpan? Duration => FinishedAt.HasValue ? FinishedAt.Value - StartedAt : null;
}
