using CsvHelper;
using CsvHelper.Configuration;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.ValueObjects;
using System.Globalization;

namespace InovaSkill.Importer.Infrastructure.Parsing;

public sealed class CsvFileParser : IFileParser
{
    public async IAsyncEnumerable<ImportedRow> ParseAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            HeaderValidated = null
        };

        using var csv = new CsvReader(reader, config);
        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];

        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                dict[header] = csv.GetField(header) ?? string.Empty;
            }

            yield return new ImportedRow(rowNumber, HeaderNormalizer.Normalize(dict));
        }
    }
}
