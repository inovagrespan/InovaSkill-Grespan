using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RouteCostPolicyTests
{
    [Theory]
    [InlineData("Mercedes Accelo", 2, 5.5, 7)]
    [InlineData("Toco 4x2", 2, 3.8, 4.5)]
    [InlineData("Truck 6x2", 3, 3.2, 4)]
    public void FindKnownVehicleConfiguration_ReturnsPreservedRanges(
        string name, int axleCount, decimal minimumEfficiency, decimal maximumEfficiency)
    {
        var result = Assert.IsType<VehicleCostConfiguration>(
            RouteCostPolicy.FindKnownVehicleConfiguration(name));

        Assert.Equal(axleCount, result.AxleCount);
        Assert.Equal(minimumEfficiency, result.MinimumFuelEfficiencyKmPerLiter);
        Assert.Equal(maximumEfficiency, result.MaximumFuelEfficiencyKmPerLiter);
    }

    [Fact]
    public void FindKnownVehicleConfiguration_UnknownType_RemainsPending() =>
        Assert.Null(RouteCostPolicy.FindKnownVehicleConfiguration("Van elétrica"));

    [Fact]
    public void CalculateFuel_UsesEfficiencyLimitsAndKeepsTotalPartsExact()
    {
        var result = RouteCostPolicy.CalculateFuel(100_000m, 5m, 10m, 6.90m);

        Assert.Equal(10.000m, result.MinimumLiters);
        Assert.Equal(20.000m, result.MaximumLiters);
        Assert.Equal(69.00m, result.MinimumCost);
        Assert.Equal(138.00m, result.MaximumCost);
        Assert.Equal(94.84m, RouteCostPolicy.RoundCurrency(result.MinimumCost + 25.84m));
    }

    [Fact]
    public void CalculateFuel_RoundsLitersBeforeCurrency()
    {
        var result = RouteCostPolicy.CalculateFuel(1_000m, 3m, 6m, 6.90m);

        Assert.Equal(0.167m, result.MinimumLiters);
        Assert.Equal(0.333m, result.MaximumLiters);
        Assert.Equal(1.15m, result.MinimumCost);
        Assert.Equal(2.30m, result.MaximumCost);
    }

    [Theory]
    [InlineData(1, 3, 4)]
    [InlineData(10, 3, 4)]
    [InlineData(2, 0, 4)]
    [InlineData(2, 5, 4)]
    public void ValidateVehicleConfiguration_RejectsInvalidLimits(int axles, decimal minimum, decimal maximum)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            RouteCostPolicy.ValidateVehicleConfiguration(axles, minimum, maximum));
    }
}
