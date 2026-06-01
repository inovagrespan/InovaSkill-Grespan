namespace InovaSkill.Importer.Domain.Entities;

public sealed class ProcessingJobEventLog
{
    public long Id { get; set; }
    public long FileJobId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public Guid CorrelationId { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
