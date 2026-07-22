using System.Globalization;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RouteOccupancyLevelPolicyTests
{
    [Theory]
    [InlineData(null, "unavailable")]
    [InlineData("0.5999", "idle")]
    [InlineData("0.60", "medium")]
    [InlineData("0.8499", "medium")]
    [InlineData("0.85", "good")]
    [InlineData("0.95", "good")]
    [InlineData("0.9501", "critical")]
    public void Classify_UsesBusinessBoundaries(string? occupancyText, string expected)
    {
        var occupancy = occupancyText is null
            ? (decimal?)null
            : decimal.Parse(occupancyText, CultureInfo.InvariantCulture);

        Assert.Equal(expected, RouteOccupancyLevelPolicy.Classify(occupancy));
    }
}
