using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Api.Contracts;

public sealed record TemplateAliasDto(string From, string To);

public sealed record TemplateConfigDto(
    long Id,
    FileType FileType,
    string Name,
    bool IsActive,
    string RequiredHeadersCsv,
    IReadOnlyList<TemplateAliasDto> Aliases);

public sealed record SaveTemplateConfigRequest(
    FileType FileType,
    string Name,
    bool IsActive,
    string RequiredHeadersCsv,
    IReadOnlyList<TemplateAliasDto> Aliases);
