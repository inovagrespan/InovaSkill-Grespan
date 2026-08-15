using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class SpreadsheetDataSourceDetector : ISpreadsheetDataSourceDetector
{
    private const int HeaderSearchRowLimit = 50;
    private static readonly string[] CustomerHeaders =
        ["CODIGO", "LOJA", "CNPJ/CPF", "NOME", "N FANTASIA", "TIPO", "ESTADO", "MUNICIPIO"];
    private static readonly string[] ProductHeaders =
        ["CODIGO", "CODONCLICK", "DESCRICAO", "TIPO", "UNIDADE", "GRUPO"];
    private static readonly string[] InventoryHeaders =
        ["CODIGO", "SALDO EM ESTOQUE", "EMPENHO PARA REQ/PV/RESERVA", "ESTOQUE DISPONIVEL"];
    private static readonly string[] CustomerRouteAssignmentHeaders = ["DIA", "MERCADO", "ROTA", "CIDADE"];
    private static readonly string[][] FiscalHeaders =
    [
        ["DOCUMENTO"], ["DATA"], ["ITEM"], ["CLIENTE", "CODIGO CLIENTE"], ["LOJA"],
        ["PRODUTO", "CODIGO PRODUTO"], ["QUANTIDADE"], ["PESO BRUTO", "PESO BRUTO KG"]
    ];
    private static readonly HashSet<string> WeekdaySheetNames =
    [
        "SEGUNDA", "SEGUNDA FEIRA", "TERCA", "TERCA FEIRA", "QUARTA", "QUARTA FEIRA",
        "QUINTA", "QUINTA FEIRA", "SEXTA", "SEXTA FEIRA", "SABADO", "DOMINGO"
    ];

    public string Detect(Stream content)
    {
        try
        {
            using var document = SpreadsheetDocument.Open(content, false);
            var workbookPart = document.WorkbookPart
                ?? throw new StructuralImportException("O XLSX não possui estrutura de workbook.");
            var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>().ToArray() ?? [];
            var snapshots = sheets.Select(sheet =>
            {
                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                return new SheetSnapshot(
                    NormalizeSheetName(sheet.Name?.Value ?? string.Empty),
                    ReadRows(worksheetPart));
            }).ToArray();
            var sharedStrings = ResolveSharedStrings(
                workbookPart.SharedStringTablePart,
                snapshots.SelectMany(sheet => sheet.Rows).SelectMany(row => row)
                    .Where(cell => cell.IsSharedString)
                    .Select(cell => cell.SharedStringIndex)
                    .ToHashSet());
            var normalizedSheets = snapshots.Select(snapshot => new
            {
                snapshot.Name,
                Rows = snapshot.Rows.Select(row => row.Select(cell =>
                    SpreadsheetParsingHelpers.NormalizeHeader(cell.Resolve(sharedStrings))).ToHashSet()).ToArray()
            }).ToArray();

            var matches = new List<string>();
            if (normalizedSheets.Any(sheet => sheet.Rows.Any(row => CustomerHeaders.All(row.Contains))))
                matches.Add(CustomerImportCodes.DataSource);
            if (normalizedSheets.Any(sheet => sheet.Rows.Any(row => ProductHeaders.All(row.Contains))))
                matches.Add(ProductImportCodes.DataSource);
            if (normalizedSheets.Any(sheet => sheet.Rows.Any(row => InventoryHeaders.All(row.Contains))))
                matches.Add(InventoryCurrentImportCodes.DataSource);
            if (normalizedSheets.Any(sheet => LooksLikeDailyInventory(sheet.Rows)))
                matches.Add(DailyInventoryImportCodes.DataSource);
            if (normalizedSheets.Any(sheet => sheet.Rows.Any(row => CustomerRouteAssignmentHeaders.All(row.Contains))))
                matches.Add(CustomerRouteAssignmentImportCodes.DataSource);
            if (normalizedSheets.Any(sheet => sheet.Rows.Any(row =>
                    FiscalHeaders.All(group => group.Any(row.Contains)))))
                matches.Add(FiscalImportCodes.DataSource);
            if (normalizedSheets.Any(sheet => WeekdaySheetNames.Contains(sheet.Name) &&
                    sheet.Rows.Any(row => row.Contains("CIDADES DA ROTA"))))
                matches.Add(RouteImportCodes.DataSource);

            return matches.Count switch
            {
                1 => matches[0],
                0 => throw new StructuralImportException(
                    "Não foi possível identificar a fonte pelo cabeçalho. Use um modelo de rotas, clientes, fiscais, produtos ou estoque."),
                _ => throw new StructuralImportException(
                    $"A planilha é ambígua e corresponde a mais de uma fonte: {string.Join(", ", matches)}.")
            };
        }
        catch (StructuralImportException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new StructuralImportException("O arquivo não é um XLSX válido ou está corrompido.", exception);
        }
    }

    private static IReadOnlyList<IReadOnlyList<RawCell>> ReadRows(WorksheetPart worksheetPart)
    {
        var rows = new List<IReadOnlyList<RawCell>>();
        using var reader = OpenXmlReader.Create(worksheetPart);
        while (reader.Read() && rows.Count < HeaderSearchRowLimit)
        {
            if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;
            var row = reader.LoadCurrentElement() as Row;
            if (row is null) continue;
            rows.Add(row.Elements<Cell>().Select(RawCell.From).ToArray());
        }
        return rows;
    }

    private static IReadOnlyDictionary<int, string> ResolveSharedStrings(
        SharedStringTablePart? part,
        IReadOnlySet<int> requestedIndexes)
    {
        var values = new Dictionary<int, string>();
        if (part is null || requestedIndexes.Count == 0) return values;
        var maximumIndex = requestedIndexes.Max();
        var index = 0;
        using var reader = OpenXmlReader.Create(part);
        while (reader.Read() && index <= maximumIndex)
        {
            if (reader.ElementType != typeof(SharedStringItem) || !reader.IsStartElement) continue;
            var item = reader.LoadCurrentElement() as SharedStringItem;
            if (item is null) continue;
            if (requestedIndexes.Contains(index)) values[index] = item.InnerText;
            index++;
        }
        return values;
    }

    private static string NormalizeSheetName(string value) =>
        MunicipalityNameNormalizer.Normalize(value).Replace("-", " ", StringComparison.Ordinal);

    private static bool LooksLikeDailyInventory(IReadOnlyList<HashSet<string>> rows)
    {
        if (rows.Count < 2) return false;
        return rows[0].Contains("COD") && rows[0].Contains("PRODUTO") && rows[0].Contains("ATUAL") &&
               rows[1].Contains("MOVIMENTACOES") && rows[1].Contains("ENTRADA") &&
               rows[1].Contains("SAIDA") && rows[1].Contains("ATUAL");
    }

    private sealed record SheetSnapshot(string Name, IReadOnlyList<IReadOnlyList<RawCell>> Rows);

    private sealed record RawCell(bool IsSharedString, int SharedStringIndex, string Value)
    {
        public static RawCell From(Cell cell)
        {
            if (cell.DataType?.Value == CellValues.SharedString &&
                int.TryParse(cell.CellValue?.Text, out var sharedStringIndex))
                return new RawCell(true, sharedStringIndex, string.Empty);
            return new RawCell(false, -1, cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty);
        }

        public string Resolve(IReadOnlyDictionary<int, string> sharedStrings) =>
            IsSharedString ? sharedStrings.GetValueOrDefault(SharedStringIndex, string.Empty) : Value;
    }
}
