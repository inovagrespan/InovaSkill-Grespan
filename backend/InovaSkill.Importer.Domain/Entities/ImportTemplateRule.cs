namespace InovaSkill.Importer.Domain.Entities;

public sealed class PreProcessorTemplateRule
{
    public long Id { get; set; }
    public long PreProcessorTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string ConfigJson { get; set; } = "{}";
    public int SortOrder { get; set; }
    public PreProcessorTemplate? PreProcessorTemplate { get; set; }
}

