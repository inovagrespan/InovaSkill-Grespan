using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddCustomerAddressCoordinatesMigrationTests
{
    [Fact]
    public void Up_CreatesCoordinateTableAndRequiredIndexes()
    {
        var builder = new Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(AddCustomerAddressCoordinates).GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(new AddCustomerAddressCoordinates(), [builder]);
        var sql = Assert.IsType<SqlOperation>(Assert.Single(builder.Operations)).Sql;
        Assert.Contains("customer_address_coordinates", sql);
        Assert.Contains("NormalizedAddress", sql);
        Assert.Contains("Status", sql);
        Assert.Contains("UNIQUE INDEX", sql);
    }
}
