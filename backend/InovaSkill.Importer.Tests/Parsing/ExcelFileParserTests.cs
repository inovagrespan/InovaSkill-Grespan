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
}
