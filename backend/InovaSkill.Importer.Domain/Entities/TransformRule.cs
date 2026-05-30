namespace InovaSkill.Importer.Domain.Entities;

public sealed class TransformRule
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<ColumnMappingTransformRule> ColumnMappings { get; set; } = [];
}

