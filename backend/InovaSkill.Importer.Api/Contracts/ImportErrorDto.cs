namespace InovaSkill.Importer.Api.Contracts;

public sealed record ImportErrorDto(
    long Id,
    long FileJobId,
    int RowNumber,
    string Column,
    string Message,
    string RecordIdentifier);
