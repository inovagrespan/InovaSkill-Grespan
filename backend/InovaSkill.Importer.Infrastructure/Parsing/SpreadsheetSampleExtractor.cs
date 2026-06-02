using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace InovaSkill.Importer.Infrastructure.Parsing;

public sealed record SpreadsheetSample(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string>> PreviewRows);

public static class SpreadsheetSampleExtractor
{
    private const int HeaderSearchLimit = 5;
    private const int DefaultPreviewRowLimit = 8;
    private static readonly string[] SupportedCsvDelimiters = [",", ";", "\t"];

    public static async Task<SpreadsheetSample> ExtractAsync(
        string fileName,
        Stream stream,
        int previewRowLimit = DefaultPreviewRowLimit,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => await ExtractCsvAsync(stream, previewRowLimit, cancellationToken),
            ".xlsx" => ExtractXlsx(stream, previewRowLimit),
            _ => throw new InvalidOperationException("Extensão de arquivo não suportada para leitura de exemplo.")
        };
    }

    private static async Task<SpreadsheetSample> ExtractCsvAsync(
        Stream stream,
        int previewRowLimit,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            DetectDelimiter = true,
            DetectDelimiterValues = SupportedCsvDelimiters,
            HeaderValidated = null,
            IgnoreBlankLines = false,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, config);
        string[] headers = [];

        for (var attempt = 1; attempt <= HeaderSearchLimit; attempt++)
        {
            if (!await csv.ReadAsync())
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var candidate = csv.Parser.Record?.Select(x => x?.Trim() ?? string.Empty).ToArray() ?? [];
            if (IsLikelyHeader(candidate))
            {
                headers = candidate.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                break;
            }
        }

        if (headers.Length == 0)
        {
            return new SpreadsheetSample([], []);
        }

        var rows = new List<IReadOnlyDictionary<string, string>>();
        while (rows.Count < previewRowLimit && await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(ReadCsvRow(csv, headers));
        }

        return new SpreadsheetSample(headers, rows);
    }

    private static SpreadsheetSample ExtractXlsx(Stream stream, int previewRowLimit)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow == 0)
        {
            return new SpreadsheetSample([], []);
        }

        List<string> headers = [];
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

            var candidate = Enumerable.Range(1, lastCell)
                .Select(i => row.Cell(i).GetString().Trim())
                .ToList();

            if (IsLikelyHeader(candidate))
            {
                headers = candidate.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                headerRowNumber = rowNumber;
                break;
            }
        }

        if (headers.Count == 0)
        {
            return new SpreadsheetSample([], []);
        }

        var rows = new List<IReadOnlyDictionary<string, string>>();
        var lastPreviewRow = Math.Min(lastRow, headerRowNumber + previewRowLimit);
        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastPreviewRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                values[headers[i]] = row.Cell(i + 1).GetString().Trim();
            }

            rows.Add(values);
        }

        return new SpreadsheetSample(headers, rows);
    }

    private static IReadOnlyDictionary<string, string> ReadCsvRow(CsvReader csv, IReadOnlyList<string> headers)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            values[headers[i]] = csv.GetField(i) ?? string.Empty;
        }

        return values;
    }

    private static bool IsLikelyHeader(IReadOnlyList<string> candidate)
    {
        var nonEmpty = candidate.Count(field => !string.IsNullOrWhiteSpace(field));
        return nonEmpty >= 2;
    }
}
