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

