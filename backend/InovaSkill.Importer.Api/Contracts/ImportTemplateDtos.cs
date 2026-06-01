using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Api.Contracts;

public sealed record PreProcessorTemplateRuleDto(
    long Id,
    string Name,
    string RuleType,
    bool IsEnabled,
    string ConfigJson,
    int SortOrder);

public sealed record PreProcessorTemplateDto(
    long Id,
    string Code,
    string Name,
    bool IsActive,
    FileType FileType,
    string FileNamePattern,
    string RequiredHeadersCsv,
    string ColumnMappingsJson,
    string ValidationRulesJson,
    IReadOnlyList<PreProcessorTemplateRuleDto> Rules);

public sealed record UpsertPreProcessorTemplateRuleRequest(
    string Name,
    string RuleType,
    bool IsEnabled,
    string ConfigJson,
    int SortOrder);

public sealed record UpsertPreProcessorTemplateRequest(
    string Code,
    string Name,
    bool IsActive,
    FileType FileType,
    string FileNamePattern,
    string RequiredHeadersCsv,
    string ColumnMappingsJson,
    string ValidationRulesJson,
    IReadOnlyList<UpsertPreProcessorTemplateRuleRequest> Rules);

public sealed record ImportTemplateFileTypeDto(
    string Id,
    string Code,
    string Name,
    string Description,
    string AllowedExtensions);

public sealed record ImportTemplateTargetFieldDto(
    string Name,
    string DisplayName,
    bool Required,
    string DataType,
    string Description);

public sealed record TransformRuleDto(
    string Id,
    string Code,
    string Name,
    string Description,
    bool RequiresParameters);

public sealed record ImportTemplateRuleDto(
    string TransformRuleId,
    int Order,
    object? ParametersJson);

public sealed record ImportTemplateColumnMappingDto(
    string SourceColumnName,
    string TargetFieldName,
    bool IsRequired,
    string? DefaultValue,
    IReadOnlyList<ImportTemplateRuleDto> TransformRules);

public sealed record ImportTemplateDto(
    string Id,
    string Name,
    string Description,
    string ImportFileTypeId,
    bool IsActive,
    IReadOnlyList<ImportTemplateColumnMappingDto> ColumnMappings);

public sealed record UpsertImportTemplateRequest(
    string Name,
    string Description,
    string ImportFileTypeId,
    IReadOnlyList<ImportTemplateColumnMappingDto> ColumnMappings);

public sealed record SpreadsheetHeaderPreviewDto(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string>> PreviewRows);

