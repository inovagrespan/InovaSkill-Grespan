using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RoutesSpreadsheetParserTests
{
    [Theory]
    [InlineData("SEGUNDA", "MONDAY")]
    [InlineData("SEGUNDA NOVA", "MONDAY")]
    [InlineData("terça", "TUESDAY")]
    [InlineData("TERCA NOVA", "TUESDAY")]
    [InlineData("QUARTA-feira", "WEDNESDAY")]
    public void ResolveWeekday_RecognizesCaseAccentAndSuffixVariations(string sheetName, string expected)
    {
        Assert.Equal(expected, RoutesSpreadsheetParser.ResolveWeekday(sheetName));
    }

    [Fact]
    public void Parse_ValidWorkbook_PreservesSequenceDuplicatesNotesAndBrazilianDecimal()
    {
        using var workbook = CreateWorkbook("SEGUNDA NOVA");
        var sheet = workbook.Worksheet(1);
        AddRouteHeader(sheet, 2, "  RIO   PRETO ");
        AddEntry(sheet, 3, "", "REGENTE FEIJO", "4", "6.762,77");
        AddEntry(sheet, 4, "Observação urgente", "REGENTE FEIJO", "2", "1.491,64");
        AddVehicleTotal(sheet, 5, "Acello", "6", "8.254,41");

        var result = Parse(workbook);

        var route = Assert.Single(result.Routes);
        Assert.Equal("RIO PRETO", route.Name);
        Assert.Equal("MONDAY", route.Weekday);
        Assert.Equal("Acelo", route.VehicleType);
        Assert.Equal(3_300m, route.VehicleCapacityKg);
        Assert.Equal(2, route.Entries.Count);
        Assert.Equal([1, 2], route.Entries.Select(x => x.Sequence));
        Assert.Equal(["REGENTE FEIJO", "REGENTE FEIJO"], route.Entries.Select(x => x.Name));
        Assert.Equal(6762.77m, route.Entries[0].AveragePerDay);
        Assert.Equal("Observação urgente", route.Entries[1].Note);
        Assert.Equal(2, result.ImportedRows);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_InvalidDeliveries_CreatesErrorWithoutConvertingToZeroAndContinues()
    {
        using var workbook = CreateWorkbook("TERÇA");
        var sheet = workbook.Worksheet(1);
        AddRouteHeader(sheet, 2, "MARÍLIA");
        AddEntry(sheet, 3, "", "PRACINHA", "B", "100,50");
        AddEntry(sheet, 4, "", "POMPEIA", "3", "200,25");
        AddVehicleTotal(sheet, 5, "Truck", "3", "300,75");

        var result = Parse(workbook);

        var error = Assert.Single(result.Errors);
        Assert.Equal("deliveries", error.Field);
        Assert.Equal("B", error.RawValue);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedRows);
        var entry = Assert.Single(Assert.Single(result.Routes).Entries);
        Assert.Equal("POMPEIA", entry.Name);
        Assert.Equal(3, entry.Deliveries);
    }

    [Fact]
    public void Parse_ResolvedCorrection_AppliesValueToOriginalWorkbook()
    {
        using var workbook = CreateWorkbook("TERÇA NOVA");
        var sheet = workbook.Worksheet(1);
        AddRouteHeader(sheet, 2, "MARÍLIA");
        AddEntry(sheet, 3, "", "PRACINHA", "B", "100,50");
        AddVehicleTotal(sheet, 4, "Toco", "2", "100,50");
        using var stream = Save(workbook);

        var result = new RoutesSpreadsheetParser().Parse(stream,
            [new SpreadsheetCorrection("TERÇA NOVA", 3, "deliveries", "2")]);

        Assert.Empty(result.Errors);
        Assert.Equal(2, Assert.Single(Assert.Single(result.Routes).Entries).Deliveries);
    }

    [Fact]
    public void Parse_TotalRow_IsNotSavedAsEntry()
    {
        using var workbook = CreateWorkbook("SEXTA");
        var sheet = workbook.Worksheet(1);
        AddRouteHeader(sheet, 2, "RIO PRETO");
        AddEntry(sheet, 3, "", "BADY BASSITT", "4", "1.000,00");
        AddVehicleTotal(sheet, 4, "Truck", "4", "1.000,00");

        var result = Parse(workbook);

        var entry = Assert.Single(Assert.Single(result.Routes).Entries);
        Assert.Equal("BADY BASSITT", entry.Name);
    }

    [Fact]
    public void Parse_WorkbookWithoutWeekday_ThrowsStructuralError()
    {
        using var workbook = CreateWorkbook("DADOS");
        using var stream = Save(workbook);

        var exception = Assert.Throws<StructuralImportException>(() =>
            new RoutesSpreadsheetParser().Parse(stream));

        Assert.Contains("Nenhuma aba", exception.Message);
    }

    private static RoutesSpreadsheetParseResult Parse(XLWorkbook workbook)
    {
        using var stream = Save(workbook);
        return new RoutesSpreadsheetParser().Parse(stream);
    }

    private static MemoryStream Save(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static XLWorkbook CreateWorkbook(string sheetName)
    {
        var workbook = new XLWorkbook();
        workbook.AddWorksheet(sheetName);
        return workbook;
    }

    private static void AddRouteHeader(IXLWorksheet sheet, int row, string name)
    {
        sheet.Cell(row, 2).Value = name;
        sheet.Cell(row, 3).Value = "CIDADES DA ROTA";
    }

    private static void AddEntry(IXLWorksheet sheet, int row, string note, string name, string deliveries, string average)
    {
        sheet.Cell(row, 2).Value = note;
        sheet.Cell(row, 3).Value = name;
        sheet.Cell(row, 4).Value = deliveries;
        sheet.Cell(row, 5).Value = average;
    }

    private static void AddVehicleTotal(IXLWorksheet sheet, int row, string vehicle, string deliveries, string average)
    {
        sheet.Cell(row, 2).Value = vehicle;
        sheet.Cell(row, 4).Value = deliveries;
        sheet.Cell(row, 5).Value = average;
    }
}
