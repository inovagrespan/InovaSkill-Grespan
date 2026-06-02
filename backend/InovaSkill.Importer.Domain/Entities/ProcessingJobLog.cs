namespace InovaSkill.Importer.Domain.Entities;

public sealed class ProcessingJobLog
{
    public long Id { get; set; }
    public long FileJobId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Stage { get; set; } = string.Empty;
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = string.Empty;
}
