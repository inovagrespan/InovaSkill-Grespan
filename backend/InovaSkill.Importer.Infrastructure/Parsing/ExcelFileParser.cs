using ClosedXML.Excel;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Infrastructure.Parsing;

public sealed class ExcelFileParser : IFileParser
{
    private const int HeaderSearchLimit = 5;

    public async IAsyncEnumerable<ImportedRow> ParseAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow == 0)
        {
            yield break;
        }

        List<string>? headers = null;
        var headerRowNumber = 0;
        var maxRowToSearch = Math.Min(HeaderSearchLimit, lastRow);

        for (var rowNumber = 1; rowNumber <= maxRowToSearch; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var lastCell = row.LastCellUsed()?.Address.ColumnNumber ?? 0;
            if (lastCell == 0)
            {
                continue;
            }

            var candidateHeaders = Enumerable.Range(1, lastCell)
                .Select(i => row.Cell(i).GetString().Trim())
                .ToList();

            if (IsLikelyHeader(candidateHeaders))
            {
                headers = candidateHeaders;
                headerRowNumber = rowNumber;
                break;
            }
        }

        if (headers is null)
        {
            throw new InvalidOperationException($"Cabeçalho não encontrado nas {HeaderSearchLimit} primeiras linhas.");
        }

        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
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

    private static bool IsLikelyHeader(IReadOnlyList<string> candidate)
    {
        var nonEmpty = candidate.Count(field => !string.IsNullOrWhiteSpace(field));
        return nonEmpty >= 2;
    }
}
