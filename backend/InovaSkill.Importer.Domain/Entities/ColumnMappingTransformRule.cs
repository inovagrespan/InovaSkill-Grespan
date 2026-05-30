namespace InovaSkill.Importer.Domain.Entities;

public sealed class ColumnMappingTransformRule
{
    public long Id { get; set; }
    public long ImportColumnMappingId { get; set; }
    public long TransformRuleId { get; set; }
    public int Order { get; set; }
    public string? ParametersJson { get; set; }
    public ImportColumnMapping? ImportColumnMapping { get; set; }
    public TransformRule? TransformRule { get; set; }
}

