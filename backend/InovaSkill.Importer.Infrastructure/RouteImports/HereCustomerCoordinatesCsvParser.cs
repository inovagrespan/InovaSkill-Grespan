using System.Globalization;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.VisualBasic.FileIO;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record ParsedHereCustomerCoordinateRow(
    int RowNumber,
    string ExternalCode,
    string CustomerName,
    string City,
    string OriginalAddress,
    string Status,
    decimal Latitude,
    decimal Longitude,
    string DisplayName,
    string State,
    string PostalCode);

public sealed record HereCustomerCoordinatesParseResult(
    IReadOnlyList<ParsedHereCustomerCoordinateRow> Rows,
    int TotalRows,
    int IgnoredRows);

public sealed class HereCustomerCoordinatesCsvParser
{
    private static readonly string[] RequiredHeaders =
        ["COD TOTVS", "STATUS HERE", "LATITUDE HERE", "LONGITUDE HERE"];
    private static readonly HashSet<string> AcceptedStatuses =
        ["NUMERO EXATO", "NUMERO INTERPOLADO"];

    public HereCustomerCoordinatesParseResult Parse(Stream content)
    {
        using var reader = new StreamReader(content, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(";");
        var headers = parser.ReadFields() ?? throw new StructuralImportException("O CSV da HERE está vazio.");
        var columns = headers.Select((value, index) => new { Name = NormalizeHeader(value), Index = index })
            .GroupBy(item => item.Name)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.Ordinal);
        var missing = RequiredHeaders.Where(header => !columns.ContainsKey(header)).ToArray();
        if (missing.Length > 0)
            throw new StructuralImportException($"Cabeçalho inválido. Colunas obrigatórias: {string.Join(", ", RequiredHeaders)}.");

        string Read(string[] fields, string header) =>
            columns.TryGetValue(header, out var index) && index < fields.Length ? fields[index].Trim() : string.Empty;
        var rows = new List<ParsedHereCustomerCoordinateRow>();
        var total = 0;
        var ignored = 0;
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? [];
            total++;
            var rowNumber = total + 1;
            var status = NormalizeHeader(Read(fields, "STATUS HERE"));
            if (!AcceptedStatuses.Contains(status)) { ignored++; continue; }
            var code = Read(fields, "COD TOTVS");
            if (string.IsNullOrWhiteSpace(code))
                throw new StructuralImportException($"Linha {rowNumber}: COD TOTVS é obrigatório.");
            if (!TryDecimal(Read(fields, "LATITUDE HERE"), out var latitude) || latitude is < -90 or > 90 ||
                !TryDecimal(Read(fields, "LONGITUDE HERE"), out var longitude) || longitude is < -180 or > 180)
                throw new StructuralImportException($"Linha {rowNumber}: latitude ou longitude HERE inválida.");
            rows.Add(new(rowNumber, code, Read(fields, "FANTASIA"), Read(fields, "CIDADE"),
                Read(fields, "ENDEREÇO ENTREGA"), status, latitude, longitude,
                Read(fields, "ENDEREÇO HERE"), Read(fields, "ESTADO HERE"), Read(fields, "CEP HERE")));
        }
        var duplicate = rows.GroupBy(row => row.ExternalCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new StructuralImportException($"COD TOTVS duplicado no CSV: {duplicate.Key}.");
        return new(rows, total, ignored);
    }

    private static bool TryDecimal(string value, out decimal parsed) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("pt-BR"), out parsed) ||
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);

    private static string NormalizeHeader(string value) =>
        MunicipalityNameNormalizer.Normalize(value.TrimStart('\uFEFF'));
}
