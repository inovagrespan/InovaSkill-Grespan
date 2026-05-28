using InovaSkill.Importer.Infrastructure.Parsing;

namespace InovaSkill.Importer.Tests.Parsing;

public class CsvFileParserTests
{
    [Fact]
    public async Task ParseAsync_ReadsRows()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "TestData", "customers-valid.csv");
        var parser = new CsvFileParser();

        var rows = new List<InovaSkill.Importer.Domain.ValueObjects.ImportedRow>();
        await foreach (var row in parser.ParseAsync(filePath, CancellationToken.None))
        {
            rows.Add(row);
        }

        Assert.Equal(2, rows.Count);
        Assert.Equal("Alice", rows[0].Get("name"));
        Assert.Equal("bob@corp.com", rows[1].Get("email"));
    }
}
