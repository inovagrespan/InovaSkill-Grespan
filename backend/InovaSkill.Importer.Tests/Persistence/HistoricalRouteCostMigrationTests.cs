using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class HistoricalRouteCostMigrationTests
{
    [Fact]
    public void Up_PreservesHistoricalCostWhenOptimizationResultIsReplaced()
    {
        var migration = new TestMigration();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        migration.ApplyUp(builder);

        var foreignKey = Assert.Single(builder.Operations.OfType<AddForeignKeyOperation>());
        Assert.Equal("route_cost_items", foreignKey.Table);
        Assert.Equal("OptimizationResultId", Assert.Single(foreignKey.Columns));
        Assert.Equal(ReferentialAction.SetNull, foreignKey.OnDelete);
    }

    [Fact]
    public void VehicleUp_PreservesHistoricalCostWhenOptimizationVehicleIsReplaced()
    {
        var migration = new TestVehicleMigration();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        migration.ApplyUp(builder);

        var foreignKey = Assert.Single(builder.Operations.OfType<AddForeignKeyOperation>());
        Assert.Equal("route_cost_items", foreignKey.Table);
        Assert.Equal("OptimizationVehicleId", Assert.Single(foreignKey.Columns));
        Assert.Equal(ReferentialAction.SetNull, foreignKey.OnDelete);
    }

    private sealed class TestMigration : PreserveHistoricalRouteCosts
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }

    private sealed class TestVehicleMigration : PreserveHistoricalRouteCostVehicle
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }
}
