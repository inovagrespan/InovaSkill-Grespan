using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record ParsedCustomerRouteAssignmentRow(
    string SheetName, int RowNumber, string Weekday, string MarketName,
    string RouteName, string MunicipalityName);

public sealed record CustomerRouteAssignmentsParseResult(
    IReadOnlyList<ParsedCustomerRouteAssignmentRow> Rows, int TotalRows);

public sealed class CustomerRouteAssignmentsSpreadsheetParser
{
    private const int HeaderSearchRowLimit = 50;
    private static readonly string[] RequiredHeaders = ["DIA", "MERCADO", "ROTA", "CIDADE"];
    private static readonly IReadOnlyDictionary<string, string> Weekdays = new Dictionary<string, string>
    {
        ["SEGUNDA"] = "MONDAY", ["SEGUNDA FEIRA"] = "MONDAY",
        ["TERCA"] = "TUESDAY", ["TERCA FEIRA"] = "TUESDAY",
        ["QUARTA"] = "WEDNESDAY", ["QUARTA FEIRA"] = "WEDNESDAY",
        ["QUINTA"] = "THURSDAY", ["QUINTA FEIRA"] = "THURSDAY",
        ["SEXTA"] = "FRIDAY", ["SEXTA FEIRA"] = "FRIDAY",
        ["SABADO"] = "SATURDAY", ["DOMINGO"] = "SUNDAY"
    };

    public CustomerRouteAssignmentsParseResult Parse(
        Stream content, IReadOnlyDictionary<(string Sheet, int Row, string Field), string>? corrections = null)
    {
        using var workbook = new XLWorkbook(content);
        var rows = new List<ParsedCustomerRouteAssignmentRow>();
        var totalRows = 0;
        foreach (var sheet in workbook.Worksheets)
        {
            var header = sheet.RowsUsed().Take(HeaderSearchRowLimit).FirstOrDefault(row =>
            {
                var headers = row.CellsUsed().Select(cell => Normalize(cell.GetFormattedString()))
                    .ToHashSet(StringComparer.Ordinal);
                return RequiredHeaders.All(headers.Contains);
            });
            if (header is null) continue;
            var columns = header.CellsUsed().ToDictionary(
                cell => Normalize(cell.GetFormattedString()), cell => cell.Address.ColumnNumber, StringComparer.Ordinal);
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber();
            for (var rowNumber = header.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
            {
                string Read(string headerName, string field)
                {
                    if (corrections?.TryGetValue((sheet.Name, rowNumber, field), out var corrected) == true)
                        return Compact(corrected);
                    return Compact(sheet.Cell(rowNumber, columns[headerName]).GetFormattedString());
                }
                var day = Read("DIA", "weekday");
                var market = Read("MERCADO", "market_name");
                var route = Read("ROTA", "route_name");
                var city = Read("CIDADE", "municipality_name");
                if (string.IsNullOrEmpty(market) && string.IsNullOrEmpty(route) && string.IsNullOrEmpty(city)) continue;
                totalRows++;
                var normalizedDay = Normalize(day);
                rows.Add(new ParsedCustomerRouteAssignmentRow(
                    sheet.Name, rowNumber, Weekdays.GetValueOrDefault(normalizedDay, day), market, route, city));
            }
        }
        if (rows.Count == 0)
            throw new StructuralImportException("Nenhuma aba com as colunas Dia, Mercado, Rota e Cidade foi encontrada.");
        return new CustomerRouteAssignmentsParseResult(rows, totalRows);
    }

    public static bool IsSupportedWeekday(string value) =>
        new[] { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" }
            .Contains(value, StringComparer.Ordinal);

    public static string Normalize(string value) => MunicipalityNameNormalizer.Normalize(value)
        .Replace('-', ' ').Replace('.', ' ').Replace('/', ' ')
        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Aggregate(string.Empty,
            (current, part) => current.Length == 0 ? part : $"{current} {part}");

    private static string Compact(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
