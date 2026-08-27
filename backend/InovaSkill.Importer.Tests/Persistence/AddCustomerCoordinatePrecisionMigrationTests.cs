using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddCustomerCoordinatePrecisionMigrationTests
{
    [Fact]
    public void Up_AddsRequiredPrecisionWithSafeDefault()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(AddCustomerCoordinatePrecision).GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(new AddCustomerCoordinatePrecision(), [builder]);
        var sql = Assert.Single(builder.Operations.OfType<SqlOperation>()).Sql;
        Assert.Contains("ADD COLUMN IF NOT EXISTS \"Precision\"", sql);
        Assert.Contains("DEFAULT 'EXACT'", sql);
    }
}
