using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IImportPreProcessingPipeline
{
    IAsyncEnumerable<PreProcessedImportRow> ProcessRowsAsync(
        ImportPreProcessingRequest request,
        CancellationToken cancellationToken);
}

public sealed record ImportPreProcessingRequest(
    string FileName,
    string? ImportFileTypeCode,
    IAsyncEnumerable<TableRow> Rows);

public sealed record PreProcessedImportRow(
    ImportedRow Row,
    string? DetectedFileTypeCode,
    IReadOnlyList<ImportPreProcessingError> Errors,
    bool ShouldStopProcessing);

public sealed record ImportPreProcessingError(
    int RowNumber,
    string Column,
    string Message);
