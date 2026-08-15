using ClosedXML.Excel;
using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class CustomerRouteAssignmentsSpreadsheetParserTests
{
    [Fact]
    public void Parse_ReadsCompatibleSheets_NormalizesWeekday_AndIgnoresSeparators()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Capa").Cell(1, 1).Value = "Relatório";
        var sheet = workbook.AddWorksheet("Rotas Atuais ");
        sheet.Cell(2, 2).Value = "Dia "; sheet.Cell(2, 3).Value = "Mercado ";
        sheet.Cell(2, 4).Value = "Rota "; sheet.Cell(2, 5).Value = "Cidade ";
        sheet.Cell(3, 2).Value = "Terça"; sheet.Cell(3, 3).Value = " Mercado  São José ";
        sheet.Cell(3, 4).Value = "Marília 1"; sheet.Cell(3, 5).Value = "Marília";
        sheet.Cell(4, 2).Value = "Terça";

        var result = Parse(workbook);

        var row = Assert.Single(result.Rows);
        Assert.Equal("TUESDAY", row.Weekday);
        Assert.Equal("Mercado São José", row.MarketName);
        Assert.Equal(1, result.TotalRows);
    }

    [Fact]
    public void Parse_PreservesIncompleteBusinessRowForReview()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Dados");
        sheet.Cell(1, 1).Value = "Dia"; sheet.Cell(1, 2).Value = "Mercado";
        sheet.Cell(1, 3).Value = "Rota"; sheet.Cell(1, 4).Value = "Cidade";
        sheet.Cell(2, 1).Value = "Segunda"; sheet.Cell(2, 2).Value = "Cliente";
        sheet.Cell(2, 3).Value = "Bauru";

        var row = Assert.Single(Parse(workbook).Rows);
        Assert.Empty(row.MunicipalityName);
    }

    private static CustomerRouteAssignmentsParseResult Parse(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream); stream.Position = 0;
        return new CustomerRouteAssignmentsSpreadsheetParser().Parse(stream);
    }
}
