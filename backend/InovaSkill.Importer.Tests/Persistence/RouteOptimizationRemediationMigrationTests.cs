using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class RouteOptimizationRemediationMigrationTests
{
    [Fact]
    public void Migration_DefinesAuditVersioningIssuesAndAccessIndexes()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestMigration().Apply(builder);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        Assert.Contains("daily_route_optimization_issues", sql);
        Assert.Contains("route_import_corrections", sql);
        Assert.Contains("municipality_aliases", sql);
        Assert.Contains("DerivedFromImportId", sql);
        Assert.Contains("InheritedFromResultId", sql);
        Assert.Contains("ParentJobExecutionId", sql);
        Assert.Contains("IsExcludedFromOptimization", sql);
        Assert.Contains("VehicleCapacityKgSnapshot", sql);
        Assert.Contains("IX_municipality_aliases_Source_NormalizedAlias", sql);
        Assert.Contains("IX_daily_route_optimization_issues_Result_Code", sql);
    }

    private sealed class TestMigration : AddRouteOptimizationRemediation
    {
        public void Apply(MigrationBuilder builder) => base.Up(builder);
    }
}
