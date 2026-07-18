using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Infrastructure.Detection;

namespace InovaSkill.Importer.Tests.Detection;

public sealed class DetectorRegistryTests
{
    [Fact]
    public void Get_WithExistingCode_ReturnsDetector()
    {
        var detector = new FixedResultDetector("MY_DETECTOR", 0, 0);
        var registry = new DetectorRegistry([detector]);

        var result = registry.Get("MY_DETECTOR");

        Assert.Same(detector, result);
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var detector = new FixedResultDetector("MY_DETECTOR", 0, 0);
        var registry = new DetectorRegistry([detector]);

        var result = registry.Get("my_detector");

        Assert.Same(detector, result);
    }

    [Fact]
    public void Get_WithNonExistentCode_Throws()
    {
        var registry = new DetectorRegistry([]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Get("NON_EXISTENT"));

        Assert.Contains("NON_EXISTENT", exception.Message);
    }

    [Fact]
    public void Get_WithEmptyCode_Throws()
    {
        var registry = new DetectorRegistry([]);

        Assert.Throws<ArgumentException>(() => registry.Get(""));
    }

    [Fact]
    public void Constructor_WithMultipleDetectors_ResolvesEachByCode()
    {
        var first = new FixedResultDetector("DETECTOR_A", 0, 0);
        var second = new FixedResultDetector("DETECTOR_B", 0, 0);
        var registry = new DetectorRegistry([first, second]);

        Assert.Same(first, registry.Get("DETECTOR_A"));
        Assert.Same(second, registry.Get("DETECTOR_B"));
    }
}
