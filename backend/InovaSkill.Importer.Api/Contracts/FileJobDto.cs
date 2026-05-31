using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Api.Contracts;

public sealed record FileJobDto(
    long Id,
    string FilePath,
    string? ImportFileTypeCode,
    FileJobStatus Status,
    DateTime CreatedAt,
    int ErrorCount,
    string CurrentStep,
    int ProgressPercent,
    int ProcessedRows,
    int TotalRows,
    string? CurrentStageCode,
    string? CurrentStageName,
    IReadOnlyList<FileJobStageProgressDto> Stages);

public sealed record FileJobStageProgressDto(
    string Code,
    string Name,
    string Status,
    int ProgressPercent,
    int ErrorCount);
