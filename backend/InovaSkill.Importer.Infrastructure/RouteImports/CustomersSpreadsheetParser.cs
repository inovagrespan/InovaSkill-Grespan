using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record ParsedCustomerRow(
    int RowNumber, string ExternalCode, string BranchCode, string DocumentNumber,
    string LegalName, string TradeName, string CustomerType, string StateCode,
    string MunicipalityName, string NormalizedMunicipalityName);

public sealed record CustomersSpreadsheetParseResult(IReadOnlyList<ParsedCustomerRow> Rows, int TotalRows);

public sealed class CustomersSpreadsheetParser
{
    private const int HeaderSearchRowLimit = 50;
    private static readonly string[] RequiredHeaders =
        ["CODIGO", "LOJA", "CNPJ/CPF", "NOME", "N FANTASIA", "TIPO", "ESTADO", "MUNICIPIO"];

    public CustomersSpreadsheetParseResult Parse(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new StructuralImportException("A planilha de clientes não possui abas.");
            var headerRow = sheet.RowsUsed()
                .Take(HeaderSearchRowLimit)
                .FirstOrDefault(row =>
                {
                    var availableHeaders = row.CellsUsed()
                        .Select(cell => MunicipalityNameNormalizer.Normalize(cell.GetFormattedString()))
                        .ToHashSet(StringComparer.Ordinal);
                    return RequiredHeaders.All(availableHeaders.Contains);
                })
                ?? throw new StructuralImportException(
                    $"Cabeçalho não encontrado. Colunas esperadas: {string.Join(", ", RequiredHeaders)}.");
            var columns = headerRow.CellsUsed().ToDictionary(
                cell => MunicipalityNameNormalizer.Normalize(cell.GetFormattedString()),
                cell => cell.Address.ColumnNumber,
                StringComparer.Ordinal);

            var rows = new List<ParsedCustomerRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
            for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
            {
                string Read(string header) => Compact(sheet.Cell(rowNumber, columns[header]).GetFormattedString());
                var externalCode = Read("CODIGO");
                var branchCode = Read("LOJA");
                var municipality = Read("MUNICIPIO");
                var state = Read("ESTADO").ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(externalCode) && string.IsNullOrWhiteSpace(branchCode) &&
                    string.IsNullOrWhiteSpace(municipality))
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(externalCode) || string.IsNullOrWhiteSpace(branchCode) ||
                    state.Length != 2 || string.IsNullOrWhiteSpace(municipality))
                {
                    throw new StructuralImportException(
                        $"Linha {rowNumber}: Código, Loja, Estado (UF) e Município são obrigatórios.");
                }
                rows.Add(new ParsedCustomerRow(
                    rowNumber, externalCode, branchCode, Read("CNPJ/CPF"), Read("NOME"),
                    Read("N FANTASIA"), Read("TIPO"), state, municipality,
                    MunicipalityNameNormalizer.Normalize(municipality)));
            }
            return new CustomersSpreadsheetParseResult(rows, rows.Count);
        }
        catch (StructuralImportException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StructuralImportException("O arquivo de clientes não é um XLSX válido ou está corrompido.", exception);
        }
    }

    private static string Compact(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
