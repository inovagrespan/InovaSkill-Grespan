namespace InovaSkill.Importer.Domain.Entities;

public sealed class ImportColumnMapping
{
    public long Id { get; set; }
    public long ImportTemplateId { get; set; }
    public string SourceColumnName { get; set; } = string.Empty;
    public string TargetFieldName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ImportTemplate? ImportTemplate { get; set; }
    public ICollection<ColumnMappingTransformRule> TransformRules { get; set; } = [];
}

