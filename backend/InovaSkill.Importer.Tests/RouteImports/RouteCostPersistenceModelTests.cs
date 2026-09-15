using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RouteCostPersistenceModelTests
{
    [Fact]
    public void Model_HasRequiredSnapshotAndLookupIndexes()
    {
        using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        var snapshot = db.Model.FindEntityType(typeof(RouteCostSnapshot))!;
        var item = db.Model.FindEntityType(typeof(RouteCostItem))!;
        var passage = db.Model.FindEntityType(typeof(RouteCostTollPassage))!;

        Assert.Contains(snapshot.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(RouteCostSnapshot.RouteImportId)]));
        Assert.Contains(item.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(RouteCostItem.SnapshotId), nameof(RouteCostItem.RouteId)]));
        Assert.Contains(item.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(RouteCostItem.SnapshotId), nameof(RouteCostItem.Scenario), nameof(RouteCostItem.Weekday)]));
        Assert.Contains(passage.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(RouteCostTollPassage.RouteCostItemId), nameof(RouteCostTollPassage.TollPlazaCode)]));
    }

    [Fact]
    public void Model_RequiresCompleteVehicleCostConfiguration()
    {
        using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        var vehicle = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(VehicleType))!;

        var constraints = vehicle.GetCheckConstraints().Select(item => item.Sql).ToArray();
        Assert.Contains(constraints, sql => sql.Contains("AxleCount") && sql.Contains("BETWEEN 2 AND 9"));
        Assert.Contains(constraints, sql => sql.Contains("MinimumFuelEfficiencyKmPerLiter") && sql.Contains("> 0"));
    }
}
