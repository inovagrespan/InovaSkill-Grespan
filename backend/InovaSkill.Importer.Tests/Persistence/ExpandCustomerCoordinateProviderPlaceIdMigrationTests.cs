using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class ExpandCustomerCoordinateProviderPlaceIdMigrationTests
{
    [Fact]
    public void Up_ExpandsProviderPlaceIdWithoutAddingAnIndex()
    {
        var migration = new ExpandCustomerCoordinateProviderPlaceId();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("ProviderPlaceId", sql);
        Assert.Contains("character varying(512)", sql);
        Assert.DoesNotContain("INDEX", sql, StringComparison.OrdinalIgnoreCase);
    }
}
