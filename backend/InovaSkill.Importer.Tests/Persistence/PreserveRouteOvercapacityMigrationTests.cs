using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class PreserveRouteOvercapacityMigrationTests
{
    [Fact]
    public void Up_RecalculatesEveryOccupancyDimensionWithoutOneHundredPercentCap()
    {
        var sql = Assert.Single(new PreserveRouteOvercapacity().UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("route.\"TotalWeightKg\" / vehicle.\"CapacityKg\"", sql);
        Assert.Contains("route.\"TotalVolumeM3\" / vehicle.\"CapacityVolumeM3\"", sql);
        Assert.Contains("route.\"TotalPallets\"::numeric / vehicle.\"CapacityPallets\"", sql);
        Assert.Contains("\"OverallOccupancy\" = GREATEST", sql);
        Assert.DoesNotContain("LEAST", sql, StringComparison.OrdinalIgnoreCase);
    }
}
