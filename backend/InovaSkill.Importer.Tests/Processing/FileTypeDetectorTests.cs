using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Processing;

namespace InovaSkill.Importer.Tests.Processing;

public class FileTypeDetectorTests
{
    private readonly FileTypeDetector _sut = new();

    [Fact]
    public void Detect_ReturnsCustomers_WhenHeaderMatches()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Alice",
            ["email"] = "alice@corp.com"
        };

        var result = _sut.DetectCode(row);

        Assert.Equal(ImportFileTypeCodes.CustomerList, result);
    }

    [Fact]
    public void Detect_ReturnsCommercialTransaction_WhenHeaderMatches()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["documentnumber"] = "NF-123",
            ["transactiondate"] = "2026-05-28",
            ["totalamount"] = "100.50"
        };

        var result = _sut.DetectCode(row);

        Assert.Equal(ImportFileTypeCodes.SalesInvoice, result);
    }

    [Fact]
    public void Detect_ReturnsCommercialTransaction_WhenPortugueseHeaderMatches()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["documento"] = "000001",
            ["cliente"] = "001091",
            ["produto"] = "10000164",
            ["total"] = "83.00"
        };

        var result = _sut.DetectCode(row);

        Assert.Equal(ImportFileTypeCodes.SalesInvoice, result);
    }

    [Fact]
    public void Detect_ReturnsUnknown_WhenHeaderDoesNotMatch()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["foo"] = "bar"
        };

        var result = _sut.DetectCode(row);

        Assert.Null(result);
    }
}
