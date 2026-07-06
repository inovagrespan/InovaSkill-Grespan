using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record SpreadsheetCorrection(string SheetName, int RowNumber, string Field, string Value);
public sealed record ParsedRouteEntry(int Sequence, string Name, int Deliveries, decimal AveragePerDay, string? Note);
public sealed record ParsedRoute(string Name, string Weekday, string VehicleType, IReadOnlyList<ParsedRouteEntry> Entries)
{
    public decimal VehicleCapacityKg { get; init; }
}
public sealed record ParsedImportError(string SheetName, int RowNumber, string Field, string RawValue, string Message);
public sealed record RoutesSpreadsheetParseResult(
    IReadOnlyList<ParsedRoute> Routes,
    IReadOnlyList<ParsedImportError> Errors,
    int TotalRows,
    int ImportedRows);

public sealed class RoutesSpreadsheetParser
{
    private const int RouteNameColumn = 2;
    private const int EntryNameColumn = 3;
    private const int DeliveriesColumn = 4;
    private const int AveragePerDayColumn = 5;

    public RoutesSpreadsheetParseResult Parse(
        Stream content,
        IReadOnlyCollection<SpreadsheetCorrection>? corrections = null)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var correctionMap = (corrections ?? [])
                .ToDictionary(
                    x => CorrectionKey(x.SheetName, x.RowNumber, x.Field),
                    x => x.Value,
                    StringComparer.OrdinalIgnoreCase);
            var routes = new List<ParsedRoute>();
            var errors = new List<ParsedImportError>();
            var totalRows = 0;
            var importedRows = 0;
            var recognizedDaySheets = 0;

            foreach (var sheet in workbook.Worksheets)
            {
                var weekday = ResolveWeekday(sheet.Name);
                if (weekday is null)
                {
                    continue;
                }

                recognizedDaySheets++;
                ParseSheet(sheet, weekday, correctionMap, routes, errors, ref totalRows, ref importedRows);
            }

            if (recognizedDaySheets == 0)
            {
                throw new StructuralImportException("Nenhuma aba correspondente a um dia da semana foi encontrada.");
            }

            if (routes.Count == 0)
            {
                throw new StructuralImportException("Nenhuma rota foi encontrada na planilha.");
            }

            return new RoutesSpreadsheetParseResult(routes, errors, totalRows, importedRows);
        }
        catch (StructuralImportException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StructuralImportException("O arquivo não é um XLSX válido ou está corrompido.", exception);
        }
    }

    private static void ParseSheet(
        IXLWorksheet sheet,
        string weekday,
        IReadOnlyDictionary<string, string> corrections,
        ICollection<ParsedRoute> routes,
        ICollection<ParsedImportError> errors,
        ref int totalRows,
        ref int importedRows)
    {
        RouteDraft? current = null;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        for (var rowNumber = 1; rowNumber <= lastRow; rowNumber++)
        {
            var routeOrNote = NormalizeText(sheet.Cell(rowNumber, RouteNameColumn).GetFormattedString());
            var entryName = NormalizeText(sheet.Cell(rowNumber, EntryNameColumn).GetFormattedString());

            if (!string.IsNullOrWhiteSpace(routeOrNote) && NormalizeToken(entryName) == "cidades da rota")
            {
                FinishRoute(current, routes, errors, corrections);
                current = new RouteDraft(routeOrNote, weekday, sheet.Name, rowNumber);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            var vehicle = NormalizeVehicleType(routeOrNote);
            if (string.IsNullOrWhiteSpace(entryName) && vehicle is not null)
            {
                current.VehicleType = vehicle;
                FinishRoute(current, routes, errors, corrections);
                current = null;
                continue;
            }

            if (string.IsNullOrWhiteSpace(entryName))
            {
                continue;
            }

            totalRows++;
            var deliveriesRaw = ResolveCellValue(sheet, rowNumber, DeliveriesColumn, "deliveries", corrections);
            var averageRaw = ResolveCellValue(sheet, rowNumber, AveragePerDayColumn, "average_per_day", corrections);
            var valid = true;

            if (!TryParseInteger(deliveriesRaw, out var deliveries))
            {
                errors.Add(new ParsedImportError(
                    sheet.Name, rowNumber, "deliveries", deliveriesRaw,
                    "Quantidade de entregas deve ser numérica."));
                valid = false;
            }

            if (!TryParseDecimal(averageRaw, out var averagePerDay))
            {
                errors.Add(new ParsedImportError(
                    sheet.Name, rowNumber, "average_per_day", averageRaw,
                    "Média/Dia deve ser numérica."));
                valid = false;
            }

            if (!valid)
            {
                continue;
            }

            current.Entries.Add(new ParsedRouteEntry(
                current.Entries.Count + 1,
                entryName,
                deliveries,
                averagePerDay,
                string.IsNullOrWhiteSpace(routeOrNote) ? null : routeOrNote));
            importedRows++;
        }

        FinishRoute(current, routes, errors, corrections);
    }

    private static string ResolveCellValue(
        IXLWorksheet sheet,
        int rowNumber,
        int columnNumber,
        string field,
        IReadOnlyDictionary<string, string> corrections)
    {
        if (corrections.TryGetValue(
            CorrectionKey(sheet.Name, rowNumber, field),
            out var correction))
        {
            return correction;
        }

        var cell = sheet.Cell(rowNumber, columnNumber);
        return cell.DataType == XLDataType.Number
            ? cell.GetValue<decimal>().ToString(CultureInfo.GetCultureInfo("pt-BR"))
            : NormalizeText(cell.GetFormattedString());
    }

    private static void FinishRoute(
        RouteDraft? draft,
        ICollection<ParsedRoute> routes,
        ICollection<ParsedImportError>? errors = null,
        IReadOnlyDictionary<string, string>? corrections = null)
    {
        if (draft is null || draft.Entries.Count == 0)
        {
            return;
        }

        var vehicleType = draft.VehicleType;
        if (vehicleType is null &&
            corrections?.TryGetValue(
                CorrectionKey(draft.SheetName, draft.HeaderRowNumber, "vehicle_type"),
                out var correctedVehicleType) == true)
        {
            vehicleType = NormalizeVehicleType(correctedVehicleType);
        }

        if (vehicleType is null)
        {
            errors?.Add(new ParsedImportError(
                draft.SheetName, draft.HeaderRowNumber, "vehicle_type", draft.Name,
                $"A rota '{draft.Name}' não possui um tipo de veículo reconhecido."));
            return;
        }

        routes.Add(new ParsedRoute(
            draft.Name,
            draft.Weekday,
            vehicleType,
            draft.Entries)
        {
            VehicleCapacityKg = draft.VehicleCapacityKg
        });
    }

    public static string? ResolveWeekday(string sheetName)
    {
        var token = NormalizeToken(sheetName);
        if (token.Contains("segunda", StringComparison.Ordinal)) return "MONDAY";
        if (token.Contains("terca", StringComparison.Ordinal)) return "TUESDAY";
        if (token.Contains("quarta", StringComparison.Ordinal)) return "WEDNESDAY";
        if (token.Contains("quinta", StringComparison.Ordinal)) return "THURSDAY";
        if (token.Contains("sexta", StringComparison.Ordinal)) return "FRIDAY";
        return null;
    }

    public static string NormalizeText(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string? NormalizeVehicleType(string value)
    {
        var token = NormalizeToken(value);
        if (string.IsNullOrWhiteSpace(token)) return null;
        if (token == "acello") return "Acelo";
        return CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(token);
    }

    public static bool TryParseInteger(string value, out int parsed) =>
        int.TryParse(NormalizeText(value), NumberStyles.Integer, CultureInfo.GetCultureInfo("pt-BR"), out parsed);

    public static bool TryParseDecimal(string value, out decimal parsed) =>
        decimal.TryParse(
            NormalizeText(value),
            NumberStyles.Number,
            CultureInfo.GetCultureInfo("pt-BR"),
            out parsed);

    private static string CorrectionKey(string sheetName, int rowNumber, string field) =>
        $"{NormalizeToken(sheetName)}|{rowNumber}|{field}";

    private static string NormalizeToken(string value)
    {
        var normalized = NormalizeText(value).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed class RouteDraft(string name, string weekday, string sheetName, int headerRowNumber)
    {
        public string Name { get; } = name;
        public string Weekday { get; } = weekday;
        public string SheetName { get; } = sheetName;
        public int HeaderRowNumber { get; } = headerRowNumber;
        public string? VehicleType { get; set; }
        public decimal VehicleCapacityKg { get; set; }
        public List<ParsedRouteEntry> Entries { get; } = [];
    }
}
