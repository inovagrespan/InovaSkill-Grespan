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

    [Fact]
    public async Task ParseAsync_SkipsBlankLinesBeforeHeader_UpToFiveLines()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"csv-with-blanks-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(filePath, "\n\n\nname,email,createdat\nAlice,alice@corp.com,2026-05-20\n");

        var parser = new CsvFileParser();
        var rows = new List<InovaSkill.Importer.Domain.ValueObjects.ImportedRow>();

        await foreach (var row in parser.ParseAsync(filePath, CancellationToken.None))
        {
            rows.Add(row);
        }

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].Get("name"));
    }

    [Fact]
    public async Task ParseAsync_SkipsTitleLineAndReadsHeaderOnSecondLine()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"csv-with-title-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(filePath, "RELATORIO DE VENDAS\nname,email,createdat\nAlice,alice@corp.com,2026-05-20\n");

        var parser = new CsvFileParser();
        var rows = new List<InovaSkill.Importer.Domain.ValueObjects.ImportedRow>();

        await foreach (var row in parser.ParseAsync(filePath, CancellationToken.None))
        {
            rows.Add(row);
        }

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].Get("name"));
    }

    [Fact]
    public async Task ParseAsync_Throws_WhenHeaderNotFoundInFirstFiveLines()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"csv-no-header-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(filePath, "titulo\nlinha\ndetalhe\nresumo\nrodape\nname,email,createdat\nAlice,alice@corp.com,2026-05-20\n");

        var parser = new CsvFileParser();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in parser.ParseAsync(filePath, CancellationToken.None))
            {
            }
        });

        Assert.Contains("Cabeçalho não encontrado", ex.Message);
    }
}
