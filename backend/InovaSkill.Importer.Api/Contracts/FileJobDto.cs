using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Api.Contracts;

public sealed record FileJobDto(
    long Id,
    string FilePath,
    FileType FileType,
    FileJobStatus Status,
    DateTime CreatedAt,
    int ErrorCount,
    string CurrentStep,
    int ProgressPercent,
    int ProcessedRows,
    int TotalRows);
