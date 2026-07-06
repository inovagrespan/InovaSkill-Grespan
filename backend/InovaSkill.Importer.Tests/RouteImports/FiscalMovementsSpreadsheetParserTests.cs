using ClosedXML.Excel;
using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class FiscalMovementsSpreadsheetParserTests
{
    [Fact]
    public void Parse_GroupsArePossibleBecauseEachRowPreservesDocumentAndItemIdentity()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Fiscal");
        var headers = new[] { "Documento", "SERIE", "Data", "Tipo", "ITEM", "CLIENTE", "LOJA", "NOME",
            "CIDADE", "PRODUTO", "DESCRICAO", "QUANTIDADE", "Peso Bruto", "Tipo Operacao Descricao" };
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        object[][] rows = [
            ["424808", "1", "20250731", "NF", "01", "001091", "01", "Mercado", "Piraju", "P1", "Produto 1", 1, 8, "VENDA"],
            ["424808", "1", "20250731", "NF", "02", "001091", "01", "Mercado", "Piraju", "P2", "Produto 2", 2, 16, "VENDA"]
        ];
        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
                sheet.Cell(row + 2, column + 1).Value = XLCellValue.FromObject(rows[row][column]);
        using var stream = new MemoryStream(); workbook.SaveAs(stream); stream.Position = 0;

        var result = new FiscalMovementsSpreadsheetParser().Parse(stream);

        Assert.Equal(2, result.Rows.Count);
        Assert.Single(result.Rows.Select(x => new { x.DocumentNumber, x.Series, x.IssueDate }).Distinct());
        Assert.Equal(["01", "02"], result.Rows.Select(x => x.ItemNumber));
        Assert.Equal(24m, result.Rows.Sum(x => x.GrossWeightKg));
    }

    [Fact]
    public void Parse_ReadsNumericCellValueInsteadOfAccountingFormattedText()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Fiscal");
        string[] headers = ["Documento", "Data", "ITEM", "CLIENTE", "LOJA", "PRODUTO", "QUANTIDADE", "Peso Bruto", "DESPESAS"];
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        object[] values = ["1", "20250731", "1", "10", "01", "P1", 1, 8, 0];
        for (var column = 0; column < values.Length; column++) sheet.Cell(2, column + 1).Value = XLCellValue.FromObject(values[column]);
        sheet.Cell(2, 9).Style.NumberFormat.Format = "_-* #,##0.00_-;_-* -#,##0.00_-;_-* \"-\"??_-;_-@_-";
        using var stream = new MemoryStream(); workbook.SaveAs(stream); stream.Position = 0;

        var row = Assert.Single(new FiscalMovementsSpreadsheetParser().Parse(stream).Rows);

        Assert.Equal(0m, row.Expenses);
    }
}
