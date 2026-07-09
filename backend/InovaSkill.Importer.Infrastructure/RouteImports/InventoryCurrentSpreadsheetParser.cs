using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record ParsedInventorySnapshotRow(
    int RowNumber,
    string ErpCode,
    string BranchCode,
    string WarehouseCode,
    decimal OnHandQuantity,
    decimal CommittedQuantity,
    decimal AvailableQuantity,
    decimal StockValue,
    decimal CommittedValue);

public sealed record InventoryCurrentParseResult(
    IReadOnlyList<ParsedInventorySnapshotRow> Rows,
    IReadOnlyList<ParsedImportError> Errors,
    int TotalRows);

public sealed class InventoryCurrentSpreadsheetParser
{
    private const int HeaderSearchRowLimit = 20;

    public InventoryCurrentParseResult Parse(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new StructuralImportException("A planilha de estoque não possui abas.");
            var header = sheet.RowsUsed().Take(HeaderSearchRowLimit).FirstOrDefault(row =>
            {
                var names = row.CellsUsed().Select(cell => SpreadsheetParsingHelpers.NormalizeHeader(cell.GetFormattedString())).ToHashSet();
                return names.Contains("CODIGO") && names.Contains("SALDO EM ESTOQUE") &&
                       names.Contains("ESTOQUE DISPONIVEL");
            }) ?? throw new StructuralImportException("Cabeçalho de estoque não encontrado.");
            var columns = header.CellsUsed().GroupBy(cell => SpreadsheetParsingHelpers.NormalizeHeader(cell.GetFormattedString()))
                .ToDictionary(group => group.Key, group => group.First().Address.ColumnNumber);
            var rows = new List<ParsedInventorySnapshotRow>();
            var errors = new List<ParsedImportError>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber();
            var totalRows = 0;
            for (var rowNumber = header.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
            {
                string Read(params string[] aliases)
                {
                    var key = aliases.Select(SpreadsheetParsingHelpers.NormalizeHeader).FirstOrDefault(columns.ContainsKey);
                    return key is null ? string.Empty : SpreadsheetParsingHelpers.Compact(sheet.Cell(rowNumber, columns[key]).GetFormattedString());
                }
                decimal Number(string field, params string[] aliases)
                {
                    var key = aliases.Select(SpreadsheetParsingHelpers.NormalizeHeader).FirstOrDefault(columns.ContainsKey);
                    if (key is null) return 0;
                    var cell = sheet.Cell(rowNumber, columns[key]);
                    if (SpreadsheetParsingHelpers.TryReadDecimal(cell, out var value)) return value;
                    errors.Add(new ParsedImportError(sheet.Name, rowNumber, field, cell.GetFormattedString(),
                        "Valor numérico inválido."));
                    return 0;
                }

                var erpCode = Read("Codigo", "Código");
                if (string.IsNullOrWhiteSpace(erpCode)) continue;
                totalRows++;
                rows.Add(new ParsedInventorySnapshotRow(
                    rowNumber,
                    erpCode,
                    Read("FL", "Filial"),
                    Read("ARMZ", "Armazem", "Armazém"),
                    Number("on_hand_quantity", "Saldo em Estoque"),
                    Number("committed_quantity", "Empenho para Req/PV/Reserva", "Empenhado"),
                    Number("available_quantity", "Estoque Disponivel", "Estoque Disponível"),
                    Number("stock_value", "Valor em Estoque"),
                    Number("committed_value", "Valor Empenhado")));
            }
            return new InventoryCurrentParseResult(rows, errors, totalRows);
        }
        catch (StructuralImportException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StructuralImportException("O arquivo de estoque não é um XLSX válido ou está corrompido.", exception);
        }
    }
}
