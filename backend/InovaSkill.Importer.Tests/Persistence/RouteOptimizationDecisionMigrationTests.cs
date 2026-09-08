using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class RouteOptimizationDecisionMigrationTests
{
    [Fact]
    public void Up_CreatesAuditableDecisionTableAndIndexes()
    {
        var migration = new AddRouteOptimizationDecisions();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;
        Assert.Contains("route_optimization_decisions", sql);
        Assert.Contains("JobExecutionId", sql);
        Assert.Contains("DecidedByUserId", sql);
        Assert.Contains("Justification", sql);
        Assert.Contains("IX_route_optimization_decisions_Status_DecidedAt", sql);
    }
}
