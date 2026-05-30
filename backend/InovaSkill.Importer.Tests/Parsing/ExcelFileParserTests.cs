using ClosedXML.Excel;
using InovaSkill.Importer.Infrastructure.Parsing;

namespace InovaSkill.Importer.Tests.Parsing;

public class ExcelFileParserTests
{
    [Fact]
    public async Task ParseAsync_ReadsRows()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"customers-{Guid.NewGuid():N}.xlsx");
        CreateWorkbook(filePath);

        var parser = new ExcelFileParser();
        var rows = new List<InovaSkill.Importer.Domain.ValueObjects.ImportedRow>();

        await foreach (var row in parser.ParseAsync(filePath, CancellationToken.None))
        {
            rows.Add(row);
        }

        Assert.Equal(2, rows.Count);
        Assert.Equal("Alice", rows[0].Get("name"));
        Assert.Equal("bob@corp.com", rows[1].Get("email"));

        File.Delete(filePath);
    }

    [Fact]
    public async Task ParseAsync_SkipsBlankRowsBeforeHeader_UpToFiveRows()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"customers-blank-rows-{Guid.NewGuid():N}.xlsx");
        CreateWorkbookWithHeaderAtRow(filePath, 4);

        var parser = new ExcelFileParser();
        var rows = new List<InovaSkill.Importer.Domain.ValueObjects.ImportedRow>();

        await foreach (var row in parser.ParseAsync(filePath, CancellationToken.None))
        {
            rows.Add(row);
        }

        Assert.Equal(2, rows.Count);
        Assert.Equal("Alice", rows[0].Get("name"));

        File.Delete(filePath);
    }

    [Fact]
    public async Task ParseAsync_SkipsTitleRowAndReadsHeaderOnSecondRow()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"customers-title-row-{Guid.NewGuid():N}.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Data");
            sheet.Cell(1, 1).Value = "RELATORIO DE VENDAS";
            sheet.Cell(2, 1).Value = "name";
            sheet.Cell(2, 2).Value = "email";
            sheet.Cell(2, 3).Value = "createdat";
            sheet.Cell(3, 1).Value = "Alice";
            sheet.Cell(3, 2).Value = "alice@corp.com";
            sheet.Cell(3, 3).Value = "2026-05-20";
            workbook.SaveAs(filePath);
        }

        var parser = new ExcelFileParser();
        var rows = new List<InovaSkill.Importer.Domain.ValueObjects.ImportedRow>();

        await foreach (var row in parser.ParseAsync(filePath, CancellationToken.None))
        {
            rows.Add(row);
        }

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].Get("name"));

        File.Delete(filePath);
    }

    [Fact]
    public async Task ParseAsync_Throws_WhenHeaderNotFoundInFirstFiveRows()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"customers-no-header-{Guid.NewGuid():N}.xlsx");
        CreateWorkbookWithHeaderAtRow(filePath, 6);

        var parser = new ExcelFileParser();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in parser.ParseAsync(filePath, CancellationToken.None))
            {
            }
        });

        Assert.Contains("Cabeçalho não encontrado", ex.Message);

        File.Delete(filePath);
    }

    private static void CreateWorkbook(string filePath)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        sheet.Cell(1, 1).Value = "name";
        sheet.Cell(1, 2).Value = "email";
        sheet.Cell(1, 3).Value = "createdat";

        sheet.Cell(2, 1).Value = "Alice";
        sheet.Cell(2, 2).Value = "alice@corp.com";
        sheet.Cell(2, 3).Value = "2026-05-20";

        sheet.Cell(3, 1).Value = "Bob";
        sheet.Cell(3, 2).Value = "bob@corp.com";
        sheet.Cell(3, 3).Value = "2026-05-21";

        workbook.SaveAs(filePath);
    }

    private static void CreateWorkbookWithHeaderAtRow(string filePath, int headerRow)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        sheet.Cell(headerRow, 1).Value = "name";
        sheet.Cell(headerRow, 2).Value = "email";
        sheet.Cell(headerRow, 3).Value = "createdat";

        sheet.Cell(headerRow + 1, 1).Value = "Alice";
        sheet.Cell(headerRow + 1, 2).Value = "alice@corp.com";
        sheet.Cell(headerRow + 1, 3).Value = "2026-05-20";

        sheet.Cell(headerRow + 2, 1).Value = "Bob";
        sheet.Cell(headerRow + 2, 2).Value = "bob@corp.com";
        sheet.Cell(headerRow + 2, 3).Value = "2026-05-21";

        workbook.SaveAs(filePath);
    }
}
