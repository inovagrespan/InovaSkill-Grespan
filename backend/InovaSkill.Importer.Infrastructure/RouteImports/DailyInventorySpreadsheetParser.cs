using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record ParsedDailyInventoryRow(
    string SheetName,
    int RowNumber,
    string OperationalCode,
    string ProductName,
    DateOnly Date,
    decimal ProductionQuantity,
    decimal OutboundQuantity,
    decimal AdjustmentQuantity,
    decimal ClosingQuantity);

public sealed record DailyInventoryParseResult(
    IReadOnlyList<ParsedDailyInventoryRow> Rows,
    IReadOnlyList<ParsedImportError> Errors,
    int TotalRows);

public sealed class DailyInventorySpreadsheetParser
{
    public DailyInventoryParseResult Parse(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var rows = new List<ParsedDailyInventoryRow>();
            var errors = new List<ParsedImportError>();
            var totalRows = 0;
            foreach (var sheet in workbook.Worksheets)
            {
                var dateColumns = FindDateColumns(sheet);
                if (dateColumns.Count == 0) continue;
                var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 2;
                for (var rowNumber = 3; rowNumber <= lastRow; rowNumber++)
                {
                    var rawOperationalCode = SpreadsheetParsingHelpers.Compact(sheet.Cell(rowNumber, 1).GetFormattedString());
                    var name = SpreadsheetParsingHelpers.Compact(sheet.Cell(rowNumber, 3).GetFormattedString());
                    if (string.IsNullOrWhiteSpace(rawOperationalCode) && string.IsNullOrWhiteSpace(name)) continue;
                    totalRows++;
                    if (string.IsNullOrWhiteSpace(rawOperationalCode) ||
                        SpreadsheetParsingHelpers.NormalizeHeader(rawOperationalCode) == "COD")
                        continue;
                    var operationalCode = ProductCodeNormalizer.NormalizeOperationalCode(rawOperationalCode);
                    var previousClosing = ReadOptionalInitialClosing(sheet.Cell(rowNumber, 5));
                    foreach (var dateColumn in dateColumns)
                    {
                        decimal? production;
                        if (dateColumn.EntranceCount > 1)
                        {
                            production = 0;
                            var allValid = true;
                            for (var i = 0; i < dateColumn.EntranceCount; i++)
                            {
                                var shiftValue = ReadQuantity(sheet, rowNumber, dateColumn.EntranceColumn + i,
                                    "production_quantity", errors);
                                if (!shiftValue.HasValue) { allValid = false; break; }
                                production += shiftValue.Value;
                            }
                            if (!allValid) production = null;
                        }
                        else
                        {
                            production = ReadQuantity(sheet, rowNumber, dateColumn.EntranceColumn, "production_quantity", errors);
                        }
                        var outbound = ReadQuantity(sheet, rowNumber, dateColumn.OutboundColumn, "outbound_quantity", errors);
                        var closing = ReadClosingQuantity(sheet, rowNumber, dateColumn.CurrentColumn, previousClosing,
                            production, outbound, errors);
                        if (production is null || outbound is null || closing is null)
                        {
                            previousClosing = closing ?? previousClosing;
                            continue;
                        }
                        if (production.Value == 0 && outbound.Value == 0 && closing.Value == 0 && previousClosing.GetValueOrDefault() == 0)
                        {
                            previousClosing = closing;
                            continue;
                        }
                        rows.Add(new ParsedDailyInventoryRow(sheet.Name, rowNumber, operationalCode, name,
                            dateColumn.Date, production.Value, outbound.Value, 0, closing.Value));
                        previousClosing = closing;
                    }
                }
            }
            return new DailyInventoryParseResult(rows, errors, totalRows);
        }
        catch (StructuralImportException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StructuralImportException("O arquivo de controle diário não é um XLSX válido ou está corrompido.", exception);
        }
    }

    private static List<DailyDateColumns> FindDateColumns(IXLWorksheet sheet)
    {
        var columns = new List<DailyDateColumns>();
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var headerEndRow = Math.Min(Math.Max(sheet.LastRowUsed()?.RowNumber() ?? 2, 2), 5);
        for (var column = 1; column <= lastColumn; column++)
        {
            if (!TryReadDate(sheet.Cell(1, column), out var date)) continue;
            for (var headerRow = 2; headerRow <= headerEndRow; headerRow++)
            {
                var entrance = SpreadsheetParsingHelpers.NormalizeHeader(sheet.Cell(headerRow, column).GetFormattedString());
                if (entrance != "ENTRADA") continue;
                var outboundCol = 0;
                var currentCol = 0;
                var entranceCount = 1;
                for (var scanCol = column + 1; scanCol <= lastColumn; scanCol++)
                {
                    var header = SpreadsheetParsingHelpers.NormalizeHeader(sheet.Cell(headerRow, scanCol).GetFormattedString());
                    if (header == "ENTRADA" && outboundCol == 0) entranceCount++;
                    else if (header == "SAIDA" && outboundCol == 0) outboundCol = scanCol;
                    else if (header == "ATUAL" && currentCol == 0 && outboundCol > 0) currentCol = scanCol;
                    if (outboundCol > 0 && currentCol > 0) break;
                }
                if (outboundCol > 0 && currentCol > 0)
                {
                    columns.Add(new DailyDateColumns(date, column, outboundCol, currentCol, entranceCount));
                    break;
                }
            }
        }
        return columns;
    }

    private static bool TryReadDate(IXLCell cell, out DateOnly date)
    {
        if (cell.TryGetValue<DateTime>(out var dateTime))
        {
            date = DateOnly.FromDateTime(dateTime);
            return true;
        }
        return DateOnly.TryParse(cell.GetFormattedString(), out date);
    }

    private static decimal? ReadOptionalInitialClosing(IXLCell cell)
    {
        if (SpreadsheetParsingHelpers.TryReadDailyQuantity(cell, out var value, out _)) return value;
        return null;
    }

    private static decimal? ReadQuantity(
        IXLWorksheet sheet,
        int rowNumber,
        int columnNumber,
        string field,
        ICollection<ParsedImportError> errors)
    {
        var cell = sheet.Cell(rowNumber, columnNumber);
        if (SpreadsheetParsingHelpers.TryReadDailyQuantity(cell, out var value, out var raw)) return value;
        errors.Add(new ParsedImportError(sheet.Name, rowNumber, field, raw,
            "Quantidade inválida ou fórmula não suportada."));
        return null;
    }

    private static decimal? ReadClosingQuantity(
        IXLWorksheet sheet,
        int rowNumber,
        int columnNumber,
        decimal? previousClosing,
        decimal? production,
        decimal? outbound,
        ICollection<ParsedImportError> errors)
    {
        var cell = sheet.Cell(rowNumber, columnNumber);
        if (cell.HasFormula && previousClosing.HasValue && production.HasValue && outbound.HasValue)
            return previousClosing.Value + production.Value - outbound.Value;
        if (SpreadsheetParsingHelpers.TryReadDailyQuantity(cell, out var value, out var raw)) return value;
        errors.Add(new ParsedImportError(sheet.Name, rowNumber, "closing_quantity", raw,
            "Estoque final inválido ou fórmula não suportada."));
        return null;
    }

    private sealed record DailyDateColumns(DateOnly Date, int EntranceColumn, int OutboundColumn, int CurrentColumn, int EntranceCount = 1);
}
