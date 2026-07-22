using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RouteOccupancyCalculatorTests
{
    [Fact]
    public void Calculate_UsesLargestAvailableDimension()
    {
        var result = RouteOccupancyCalculator.Calculate(new RouteOccupancyInput(
            4_000m, 10_000m,
            85m, 100m,
            18, 20));

        Assert.Equal(0.4m, result.WeightOccupancy);
        Assert.Equal(0.85m, result.VolumeOccupancy);
        Assert.Equal(0.9m, result.PalletOccupancy);
        Assert.Equal(0.9m, result.OverallOccupancy);
        Assert.True(result.HasAvailableCapacity);
    }

    [Fact]
    public void Calculate_LimitsOccupancyToOneHundredPercent()
    {
        var result = RouteOccupancyCalculator.Calculate(new RouteOccupancyInput(
            12_500m, 10_000m,
            null, null,
            null, null));

        Assert.Equal(1m, result.WeightOccupancy);
        Assert.Equal(1m, result.OverallOccupancy);
    }

    [Fact]
    public void Calculate_IgnoresUnavailableAndInvalidCapacities()
    {
        var result = RouteOccupancyCalculator.Calculate(new RouteOccupancyInput(
            6_000m, 10_000m,
            80m, 0m,
            18, null));

        Assert.Equal(0.6m, result.WeightOccupancy);
        Assert.Null(result.VolumeOccupancy);
        Assert.Null(result.PalletOccupancy);
        Assert.Equal(0.6m, result.OverallOccupancy);
    }

    [Fact]
    public void Calculate_NoValidCapacity_MarksResultAsUnavailable()
    {
        var result = RouteOccupancyCalculator.Calculate(new RouteOccupancyInput(
            6_000m, null,
            80m, -1m,
            18, 0));

        Assert.Null(result.OverallOccupancy);
        Assert.False(result.HasAvailableCapacity);
    }

    [Fact]
    public void Calculate_ZeroLoadWithValidCapacity_ReturnsZero()
    {
        var result = RouteOccupancyCalculator.Calculate(new RouteOccupancyInput(
            0m, 10_000m,
            null, null,
            null, null));

        Assert.Equal(0m, result.WeightOccupancy);
        Assert.Equal(0m, result.OverallOccupancy);
    }

    [Fact]
    public void Calculate_DoesNotRoundBusinessValueBeforePersistence()
    {
        var result = RouteOccupancyCalculator.Calculate(new RouteOccupancyInput(
            1m, 3m,
            null, null,
            null, null));

        Assert.Equal(1m / 3m, result.WeightOccupancy);
        Assert.Equal(result.WeightOccupancy, result.OverallOccupancy);
    }
}
