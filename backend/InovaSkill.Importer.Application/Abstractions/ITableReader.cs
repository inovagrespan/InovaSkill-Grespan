using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Application.Abstractions;

public interface ITableReader
{
    IAsyncEnumerable<TableRow> ReadRowsAsync(Stream stream, CancellationToken cancellationToken);
}
