using CsvHelper;
using CsvHelper.Configuration;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.ValueObjects;
using System.Globalization;

namespace InovaSkill.Importer.Infrastructure.Parsing;

public sealed class CsvFileParser : IFileParser, ITableReader
{
    private const int HeaderSearchLimit = 5;

    public async IAsyncEnumerable<ImportedRow> ParseAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        await foreach (var row in ReadRowsAsync(stream, cancellationToken))
        {
            yield return new ImportedRow(row.RowNumber, row.Values);
        }
    }

    public async IAsyncEnumerable<TableRow> ReadRowsAsync(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            HeaderValidated = null,
            IgnoreBlankLines = false
        };

        using var csv = new CsvReader(reader, config);
        string[] headers = [];
        var headerFound = false;
        for (var attempt = 1; attempt <= HeaderSearchLimit; attempt++)
        {
            if (!await csv.ReadAsync())
            {
                break;
            }

            var candidate = csv.Parser.Record ?? [];
            if (IsLikelyHeader(candidate))
            {
                headers = candidate.Select(x => x?.Trim() ?? string.Empty).ToArray();
                headerFound = true;
                break;
            }
        }

        if (!headerFound)
        {
            throw new InvalidOperationException($"Cabeçalho não encontrado nas {HeaderSearchLimit} primeiras linhas.");
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowNumber = csv.Parser.Row;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
            {
                var header = headers[i];
                dict[header] = csv.GetField(i) ?? string.Empty;
            }

            yield return new TableRow(rowNumber, HeaderNormalizer.Normalize(dict));
        }
    }

    private static bool IsLikelyHeader(IReadOnlyList<string> candidate)
    {
        var nonEmpty = candidate.Count(field => !string.IsNullOrWhiteSpace(field));
        return nonEmpty >= 2;
    }
}
