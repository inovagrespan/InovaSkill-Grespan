using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class ImportTemplate
{
    public long Id { get; set; }
    public Guid ImportFileTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string FileNamePattern { get; set; } = string.Empty;
    public string RequiredHeadersCsv { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ImportFileType? ImportFileType { get; set; }
    public ICollection<ImportColumnMapping> ColumnMappings { get; set; } = [];
}

public sealed class PreProcessorTemplate
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public FileType FileType { get; set; } = FileType.Unknown;
    public string FileNamePattern { get; set; } = string.Empty;
    public string RequiredHeadersCsv { get; set; } = string.Empty;
    public string ColumnMappingsJson { get; set; } = string.Empty;
    public string ValidationRulesJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PreProcessorTemplateRule> Rules { get; set; } = [];
}

