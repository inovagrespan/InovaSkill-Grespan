using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IFileParser
{
    IAsyncEnumerable<ImportedRow> ParseAsync(string filePath, CancellationToken cancellationToken);
}
