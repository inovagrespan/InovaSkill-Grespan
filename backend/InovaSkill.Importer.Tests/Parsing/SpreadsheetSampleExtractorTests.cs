using InovaSkill.Importer.Infrastructure.Parsing;
using System.Text;

namespace InovaSkill.Importer.Tests.Parsing;

public class SpreadsheetSampleExtractorTests
{
    [Fact]
    public async Task ExtractAsync_ShouldDetectHeadersAndPreviewRows()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("CLIENTE;PRODUTO;QUANTIDADE\nACME;SKU-1;2\n"));

        var sample = await SpreadsheetSampleExtractor.ExtractAsync("vendas.csv", stream);

        Assert.Equal(["CLIENTE", "PRODUTO", "QUANTIDADE"], sample.Headers);
        Assert.Single(sample.PreviewRows);
        Assert.Equal("ACME", sample.PreviewRows[0]["CLIENTE"]);
    }
}
