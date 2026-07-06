using System.Globalization;
using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed record ParsedFiscalMovementRow(
    int RowNumber, string DocumentNumber, string Series, string DocumentType, DateOnly IssueDate,
    string ItemNumber, string CustomerCode, string BranchCode, string CustomerName, string CityName,
    string StateCode, string OperationCode, string OperationDescription, string? OriginalDocumentNumber,
    string ProductCode, string ProductDescription, string ProductGroupCode, string ProductGroupDescription,
    decimal Quantity, decimal GrossWeightKg, decimal? UnitValue, decimal? SourceTotalValue,
    decimal? Expenses, decimal? Ipi, decimal? Icms, decimal? Iss, string? CfopCode,
    string? CfopDescription, string? TesCode, string? TesDescription, string? OrderNumber,
    string? WarehouseCode);

public sealed record FiscalMovementsParseResult(IReadOnlyList<ParsedFiscalMovementRow> Rows, int TotalRows);

public sealed class FiscalMovementsSpreadsheetParser
{
    private const int HeaderSearchRowLimit = 50;
    private static readonly string[][] Required =
    [
        ["DOCUMENTO"], ["DATA"], ["ITEM"], ["CLIENTE", "CODIGO CLIENTE"], ["LOJA"],
        ["PRODUTO", "CODIGO PRODUTO"], ["QUANTIDADE"], ["PESO BRUTO", "PESO BRUTO KG"]
    ];

    public FiscalMovementsParseResult Parse(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new StructuralImportException("A planilha fiscal não possui abas.");
            var header = sheet.RowsUsed().Take(HeaderSearchRowLimit).FirstOrDefault(row =>
            {
                var names = row.CellsUsed().Select(cell => Normalize(cell.GetFormattedString())).ToHashSet();
                return Required.All(group => group.Any(names.Contains));
            }) ?? throw new StructuralImportException("Cabeçalho fiscal não encontrado ou incompleto.");
            var columns = header.CellsUsed().GroupBy(cell => Normalize(cell.GetFormattedString()))
                .ToDictionary(group => group.Key, group => group.First().Address.ColumnNumber);
            var rows = new List<ParsedFiscalMovementRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber();
            for (var rowNumber = header.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
            {
                IXLCell? Cell(params string[] aliases)
                {
                    var alias = aliases.Select(Normalize).FirstOrDefault(columns.ContainsKey);
                    return alias is null ? null : sheet.Cell(rowNumber, columns[alias]);
                }
                string Read(params string[] aliases)
                {
                    return Cell(aliases) is { } cell ? Compact(cell.GetFormattedString()) : string.Empty;
                }
                decimal Number(bool required, params string[] aliases)
                {
                    var cell = Cell(aliases);
                    if (cell is not null && cell.TryGetValue<decimal>(out var numericValue)) return numericValue;
                    var raw = cell is null ? string.Empty : Compact(cell.GetString());
                    if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var value) ||
                        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return value;
                    if (!required && string.IsNullOrWhiteSpace(raw)) return 0;
                    throw new StructuralImportException($"Linha {rowNumber}: valor numérico inválido em {aliases[0]}.");
                }
                decimal? OptionalNumber(params string[] aliases)
                {
                    var raw = Read(aliases);
                    if (string.IsNullOrWhiteSpace(raw)) return null;
                    return Number(true, aliases);
                }
                var document = Read("DOCUMENTO");
                if (string.IsNullOrWhiteSpace(document)) continue;
                var rawDate = Read("DATA");
                if (!TryDate(rawDate, out var date))
                    throw new StructuralImportException($"Linha {rowNumber}: data fiscal inválida '{rawDate}'.");
                rows.Add(new ParsedFiscalMovementRow(
                    rowNumber, document, Read("SERIE"), Read("TIPO"), date,
                    Read("ITEM"), Read("CLIENTE", "CODIGO CLIENTE"), Read("LOJA"), Read("NOME", "NOME CLIENTE"),
                    Read("CIDADE"), Read("UF", "ESTADO"), Read("TIPO OPERACAO"),
                    Read("TIPO OPERACAO DESCRICAO", "TIPO"), Null(Read("DOCUMENTO ORIGINAL", "NF ORIG.")),
                    Read("PRODUTO", "CODIGO PRODUTO"), Read("DESCRICAO", "DESCRICAO PRODUTO"),
                    Read("GRUPO PROD."), Read("GRUPO DESCRICAO", "GRUPO PRODUTO"),
                    Number(true, "QUANTIDADE"), Number(true, "PESO BRUTO", "PESO BRUTO KG"),
                    OptionalNumber("VLR. UNIT.", "VALOR UNITARIO"), OptionalNumber("TOTAL", "VALOR TOTAL"),
                    OptionalNumber("DESPESAS"), OptionalNumber("IPI"), OptionalNumber("ICMS"), OptionalNumber("ISS"),
                    Null(Read("CFOP")), Null(Read("DESCRICAO DO CFOP", "CFOP DESCR.")), Null(Read("TES")),
                    Null(Read("DESCRICAO DA TES", "TES TXT PADRAO")), Null(Read("PEDIDO")), Null(Read("ARMAZEM"))));
            }
            return new FiscalMovementsParseResult(rows, rows.Count);
        }
        catch (StructuralImportException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StructuralImportException("O arquivo fiscal não é um XLSX válido ou está corrompido.", exception);
        }
    }

    private static bool TryDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, ["yyyyMMdd", "dd/MM/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    private static string Normalize(string value) => MunicipalityNameNormalizer.Normalize(value);
    private static string Compact(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
