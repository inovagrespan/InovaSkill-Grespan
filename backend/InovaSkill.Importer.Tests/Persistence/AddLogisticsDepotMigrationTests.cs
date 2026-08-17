using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddLogisticsDepotMigrationTests
{
    [Fact]
    public void Up_CreatesSingletonWithCoordinateConstraints()
    {
        var migration = new AddLogisticsDepot();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("CREATE TABLE logistics_depots", sql);
        Assert.Contains("UX_logistics_depots_singleton", sql);
        Assert.Contains("Latitude\" BETWEEN -90 AND 90", sql);
        Assert.Contains("Longitude\" BETWEEN -180 AND 180", sql);
    }
}
