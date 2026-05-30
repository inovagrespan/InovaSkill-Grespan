using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IImportMappingEngine
{
    ImportMappingResult MapRow(int rowNumber, IReadOnlyDictionary<string, object?> rawValues, ImportTemplate template);
}

public sealed record ImportMappingResult(
    int RowNumber,
    IReadOnlyDictionary<string, object?> StandardValues,
    IReadOnlyList<ImportMappingError> Errors);

public sealed record ImportMappingError(
    int RowNumber,
    string Column,
    string Message);

