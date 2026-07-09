using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record ParsedProductRow(
    int RowNumber,
    string ErpCode,
    string OperationalCode,
    string Name,
    string Type,
    string Unit,
    string GroupCode,
    decimal? NetWeightKg,
    decimal? GrossWeightKg,
    string Gtin);

public sealed record ProductsParseResult(
    IReadOnlyList<ParsedProductRow> Rows,
    IReadOnlyList<ParsedImportError> Errors,
    int TotalRows);

public sealed class ProductsSpreadsheetParser
{
    private const int HeaderSearchRowLimit = 20;

    public ProductsParseResult Parse(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new StructuralImportException("A planilha de produtos não possui abas.");
            var header = sheet.RowsUsed().Take(HeaderSearchRowLimit).FirstOrDefault(row =>
            {
                var names = row.CellsUsed().Select(cell => SpreadsheetParsingHelpers.NormalizeHeader(cell.GetFormattedString())).ToHashSet();
                return names.Contains("CODIGO") && names.Contains("CODONCLICK") && names.Contains("DESCRICAO");
            }) ?? throw new StructuralImportException("Cabeçalho de produtos não encontrado.");
            var columns = header.CellsUsed().GroupBy(cell => SpreadsheetParsingHelpers.NormalizeHeader(cell.GetFormattedString()))
                .ToDictionary(group => group.Key, group => group.First().Address.ColumnNumber);
            var rows = new List<ParsedProductRow>();
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
                decimal? OptionalDecimal(string field, params string[] aliases)
                {
                    var key = aliases.Select(SpreadsheetParsingHelpers.NormalizeHeader).FirstOrDefault(columns.ContainsKey);
                    if (key is null) return null;
                    var cell = sheet.Cell(rowNumber, columns[key]);
                    if (cell.IsEmpty()) return null;
                    if (SpreadsheetParsingHelpers.TryReadDecimal(cell, out var value)) return value;
                    errors.Add(new ParsedImportError(sheet.Name, rowNumber, field, cell.GetFormattedString(),
                        "Peso em formato inválido."));
                    return null;
                }

                var erpCode = Read("Codigo", "Código");
                var name = Read("Descricao", "Descrição");
                if (string.IsNullOrWhiteSpace(erpCode) && string.IsNullOrWhiteSpace(name)) continue;
                totalRows++;
                if (string.IsNullOrWhiteSpace(erpCode))
                {
                    errors.Add(new ParsedImportError(sheet.Name, rowNumber, "erp_code", erpCode,
                        "Produto sem código ERP."));
                    continue;
                }
                rows.Add(new ParsedProductRow(
                    rowNumber,
                    erpCode,
                    ProductCodeNormalizer.NormalizeOperationalCode(Read("CodOnClick", "Cod OnClick", "Cod.OnClick")),
                    name,
                    Read("Tipo"),
                    Read("Unidade"),
                    Read("Grupo"),
                    OptionalDecimal("net_weight_kg", "Peso Liquido", "Peso Líquido"),
                    OptionalDecimal("gross_weight_kg", "Peso Bruto"),
                    Read("Cod GTIN", "Cod. GTIN", "GTIN")));
            }
            return new ProductsParseResult(rows, errors, totalRows);
        }
        catch (StructuralImportException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StructuralImportException("O arquivo de produtos não é um XLSX válido ou está corrompido.", exception);
        }
    }
}
