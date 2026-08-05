using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
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

    // Mantido para consumidores pequenos e testes. O processamento real usa StreamRows e não materializa o arquivo.
    public FiscalMovementsParseResult Parse(Stream content)
    {
        try
        {
            var rows = StreamRows(content).ToArray();
            return new FiscalMovementsParseResult(rows, rows.Length);
        }
        catch (StructuralImportException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StructuralImportException("O arquivo fiscal não é um XLSX válido ou está corrompido.", exception);
        }
    }

    public IEnumerable<ParsedFiscalMovementRow> StreamRows(
        Stream content,
        Action<int>? totalRowsDetected = null)
    {
        using var document = SpreadsheetDocument.Open(content, false);
        var workbookPart = document.WorkbookPart
            ?? throw new StructuralImportException("O XLSX fiscal não possui estrutura de workbook.");
        var firstSheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new StructuralImportException("A planilha fiscal não possui abas.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id!);
        var sharedStrings = ReadSharedStrings(workbookPart.SharedStringTablePart);
        Dictionary<string, int>? columns = null;
        var headerRowNumber = 0;
        var lastDimensionRow = 0;

        using var reader = OpenXmlReader.Create(worksheetPart);
        while (reader.Read())
        {
            if (reader.ElementType == typeof(SheetDimension) && reader.IsStartElement)
            {
                var dimension = reader.LoadCurrentElement() as SheetDimension;
                lastDimensionRow = ParseLastRowNumber(dimension?.Reference?.Value);
                continue;
            }
            if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;
            var row = reader.LoadCurrentElement() as Row;
            if (row is null) continue;
            var rowNumber = checked((int)(row.RowIndex?.Value ?? 0));
            var values = ReadRow(row, sharedStrings);

            if (columns is null)
            {
                if (rowNumber > HeaderSearchRowLimit) break;
                var names = values.Values.Select(Normalize).ToHashSet();
                if (!Required.All(group => group.Any(names.Contains))) continue;
                columns = values.GroupBy(cell => Normalize(cell.Value))
                    .Where(group => group.Key.Length > 0)
                    .ToDictionary(group => group.Key, group => group.First().Key);
                headerRowNumber = rowNumber;
                if (lastDimensionRow > headerRowNumber)
                    totalRowsDetected?.Invoke(lastDimensionRow - headerRowNumber);
                continue;
            }

            string Read(params string[] aliases)
            {
                var alias = aliases.Select(Normalize).FirstOrDefault(columns.ContainsKey);
                return alias is not null && values.TryGetValue(columns[alias], out var value) ? Compact(value) : string.Empty;
            }
            decimal Number(bool required, params string[] aliases)
            {
                var raw = Read(aliases);
                const NumberStyles spreadsheetNumberStyles = NumberStyles.Float | NumberStyles.AllowThousands;
                if (decimal.TryParse(raw, spreadsheetNumberStyles, CultureInfo.InvariantCulture, out var value) ||
                    decimal.TryParse(raw, spreadsheetNumberStyles, CultureInfo.GetCultureInfo("pt-BR"), out value))
                    return value;
                if (!required && string.IsNullOrWhiteSpace(raw)) return 0;
                throw new StructuralImportException($"Linha {rowNumber}: valor numérico inválido em {aliases[0]} ('{raw}').");
            }
            decimal? OptionalNumber(params string[] aliases)
            {
                var raw = Read(aliases);
                return string.IsNullOrWhiteSpace(raw) ? null : Number(true, aliases);
            }

            var documentNumber = Read("DOCUMENTO");
            if (string.IsNullOrWhiteSpace(documentNumber)) continue;
            var rawDate = Read("DATA");
            if (!TryDate(rawDate, out var issueDate))
                throw new StructuralImportException($"Linha {rowNumber}: data fiscal inválida '{rawDate}'.");
            yield return new ParsedFiscalMovementRow(
                rowNumber, documentNumber, Read("SERIE"), Read("TIPO"), issueDate,
                Read("ITEM"), Read("CLIENTE", "CODIGO CLIENTE"), Read("LOJA"), Read("NOME", "NOME CLIENTE"),
                Read("CIDADE"), Read("UF", "ESTADO"), Read("TIPO OPERACAO"),
                Read("TIPO OPERACAO DESCRICAO", "TIPO"), Null(Read("DOCUMENTO ORIGINAL", "NF ORIG.")),
                Read("PRODUTO", "CODIGO PRODUTO"), Read("DESCRICAO", "DESCRICAO PRODUTO"),
                Read("GRUPO PROD."), Read("GRUPO DESCRICAO", "GRUPO PRODUTO"),
                Number(true, "QUANTIDADE"), Number(true, "PESO BRUTO", "PESO BRUTO KG"),
                OptionalNumber("VLR. UNIT.", "VALOR UNITARIO"), OptionalNumber("TOTAL", "VALOR TOTAL"),
                OptionalNumber("DESPESAS"), OptionalNumber("IPI"), OptionalNumber("ICMS"), OptionalNumber("ISS"),
                Null(Read("CFOP")), Null(Read("DESCRICAO DO CFOP", "CFOP DESCR.")), Null(Read("TES")),
                Null(Read("DESCRICAO DA TES", "TES TXT PADRAO")), Null(Read("PEDIDO")), Null(Read("ARMAZEM")));
        }

        if (columns is null)
            throw new StructuralImportException("Cabeçalho fiscal não encontrado ou incompleto.");
    }

    private static IReadOnlyList<string> ReadSharedStrings(SharedStringTablePart? part)
    {
        if (part is null) return [];
        var values = new List<string>();
        using var reader = OpenXmlReader.Create(part);
        while (reader.Read())
        {
            if (reader.ElementType != typeof(SharedStringItem) || !reader.IsStartElement) continue;
            if (reader.LoadCurrentElement() is SharedStringItem item) values.Add(item.InnerText);
        }
        return values;
    }

    private static Dictionary<int, string> ReadRow(Row row, IReadOnlyList<string> sharedStrings)
    {
        var values = new Dictionary<int, string>();
        foreach (var cell in row.Elements<Cell>())
        {
            var column = ParseColumnNumber(cell.CellReference?.Value);
            if (column <= 0) continue;
            var raw = cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;
            if (cell.DataType?.Value == CellValues.SharedString &&
                int.TryParse(cell.CellValue?.Text, out var index) && index >= 0 && index < sharedStrings.Count)
                raw = sharedStrings[index];
            else if (cell.DataType?.Value == CellValues.InlineString)
                raw = cell.InlineString?.InnerText ?? string.Empty;
            else if (cell.DataType?.Value == CellValues.Boolean)
                raw = cell.CellValue?.Text == "1" ? "TRUE" : "FALSE";
            values[column] = raw;
        }
        return values;
    }

    private static int ParseColumnNumber(string? reference)
    {
        var column = 0;
        foreach (var character in reference ?? string.Empty)
        {
            if (!char.IsLetter(character)) break;
            column = checked(column * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }
        return column;
    }

    private static int ParseLastRowNumber(string? reference)
    {
        var lastCell = (reference ?? string.Empty).Split(':').LastOrDefault() ?? string.Empty;
        var digits = new string(lastCell.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var rowNumber) ? rowNumber : 0;
    }

    private static bool TryDate(string value, out DateOnly date)
    {
        if (DateOnly.TryParseExact(value, ["yyyyMMdd", "dd/MM/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date)) return true;
        if (double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var serialDate) &&
            serialDate is >= 1 and <= 2_958_465)
        {
            date = DateOnly.FromDateTime(DateTime.FromOADate(serialDate));
            return true;
        }
        return false;
    }

    private static string Normalize(string value) => MunicipalityNameNormalizer.Normalize(value);
    private static string Compact(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
