using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class OptimizationStopCustomerMigrationTests
{
    [Fact]
    public void Migration_AddsOptionalCustomerForeignKeyAndLookupIndex()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestMigration().Apply(builder);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        Assert.Contains("CustomerId", sql);
        Assert.Contains("REFERENCES customers", sql);
        Assert.Contains("IX_daily_route_optimization_stops_CustomerId", sql);
    }

    private sealed class TestMigration : AddOptimizationStopCustomer
    {
        public void Apply(MigrationBuilder builder) => base.Up(builder);
    }
}
