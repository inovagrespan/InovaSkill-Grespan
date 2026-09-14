using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddRouteOptimizationJobLookupIndexMigrationTests
{
    [Fact]
    public void MigrationCreatesAndRemovesSnapshotPollingIndex()
    {
        var migration = new AddRouteOptimizationJobLookupIndex();
        var up = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(AddRouteOptimizationJobLookupIndex)
            .GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(migration, [up]);
        var created = Assert.IsType<CreateIndexOperation>(Assert.Single(up.Operations));

        Assert.Equal("job_executions", created.Table);
        Assert.Equal(["JobType", "RelatedEntityId", "CreatedAt"], created.Columns);
        Assert.False(created.IsUnique);

        var down = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(AddRouteOptimizationJobLookupIndex)
            .GetMethod("Down", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(migration, [down]);
        var removed = Assert.IsType<DropIndexOperation>(Assert.Single(down.Operations));
        Assert.Equal(created.Name, removed.Name);
        Assert.Equal(created.Table, removed.Table);
    }
}
