using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddRegistrationAddressStreetTypeMigrationTests
{
    [Fact]
    public void Up_AddsStreetTypeAndInvalidatesDerivedCoordinates()
    {
        var migration = new AddRegistrationAddressStreetType();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;
        Assert.Contains("StreetType", sql);
        Assert.Contains("DELETE FROM customer_address_coordinates", sql);
    }
}
