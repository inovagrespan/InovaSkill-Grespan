using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class RemoveRouteOptimizationMigrationTests
{
    [Fact]
    public void Up_RemovesSchedulesAndOptimizationTables()
    {
        var migration = new RemoveRouteOptimization();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("DELETE FROM job_schedules", sql, StringComparison.Ordinal);
        Assert.Contains("'ROUTE_OPTIMIZATION'", sql, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE IF EXISTS route_optimization_scenarios", sql, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE IF EXISTS route_optimization_runs", sql, StringComparison.Ordinal);
    }
}
