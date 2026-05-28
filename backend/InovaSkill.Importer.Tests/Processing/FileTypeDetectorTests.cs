using InovaSkill.Importer.Domain.Enums;
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

        var result = _sut.Detect(row);

        Assert.Equal(FileType.Customers, result);
    }

    [Fact]
    public void Detect_ReturnsUnknown_WhenHeaderDoesNotMatch()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["foo"] = "bar"
        };

        var result = _sut.Detect(row);

        Assert.Equal(FileType.Unknown, result);
    }
}
