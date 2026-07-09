namespace InovaSkill.Importer.Domain.Entities;

public sealed class FindingEvidence
{
    public Guid Id { get; set; }
    public Guid FindingId { get; set; }
    public Finding? Finding { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? ReferenceValue { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public DateTime ObservedAt { get; set; }
}
