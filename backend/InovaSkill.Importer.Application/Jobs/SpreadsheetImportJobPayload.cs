namespace InovaSkill.Importer.Application.Jobs;

public sealed record SpreadsheetImportJobPayload(
    long FileJobId,
    string OriginalFileName,
    string? ImportFileTypeCode);
