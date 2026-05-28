using ClosedXML.Excel;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Infrastructure.Parsing;

public sealed class ExcelFileParser : IFileParser
{
    public async IAsyncEnumerable<ImportedRow> ParseAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow <= 1)
        {
            yield break;
        }

        var headerRow = worksheet.Row(1);
        var headers = headerRow.CellsUsed().Select(c => c.GetString().Trim()).ToList();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = worksheet.Row(rowNumber);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < headers.Count; i++)
            {
                dict[headers[i]] = row.Cell(i + 1).GetString().Trim();
            }

            yield return new ImportedRow(rowNumber, HeaderNormalizer.Normalize(dict));
            await Task.Yield();
        }
    }
}
