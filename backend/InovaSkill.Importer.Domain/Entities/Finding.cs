namespace InovaSkill.Importer.Domain.Entities;

public sealed class Finding
{
    public Guid Id { get; set; }
    public Guid DetectionRunId { get; set; }
    public DetectionRun? DetectionRun { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string? SubjectLabel { get; set; }
    public DateTime DetectedAt { get; set; }
}
