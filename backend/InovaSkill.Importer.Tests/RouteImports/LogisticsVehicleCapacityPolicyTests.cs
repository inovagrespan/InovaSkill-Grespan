using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class LogisticsVehicleCapacityPolicyTests
{
    [Theory]
    [InlineData("Truck", 10_300)]
    [InlineData("truck", 10_300)]
    [InlineData("Toco", 7_700)]
    [InlineData("Acelo", 3_300)]
    public void FindWeightCapacityKg_KnownVehicle_ReturnsDomainCapacity(
        string vehicleType,
        decimal expectedCapacity)
    {
        Assert.Equal(expectedCapacity, LogisticsVehicleCapacityPolicy.FindWeightCapacityKg(vehicleType));
    }

    [Fact]
    public void FindWeightCapacityKg_UnknownVehicle_DoesNotInventCapacity()
    {
        Assert.Null(LogisticsVehicleCapacityPolicy.FindWeightCapacityKg("Carreta desconhecida"));
    }
}
