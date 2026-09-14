using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddRouteDepartureTimeMigrationTests
{
    [Fact]
    public void MigrationDeclaresNullableTimeColumn()
    {
        var migration = new AddRouteDepartureTime();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.GetType().GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var operation = Assert.Single(builder.Operations.OfType<AddColumnOperation>());
        Assert.Equal("routes", operation.Table);
        Assert.Equal("DepartureTime", operation.Name);
        Assert.Equal("time without time zone", operation.ColumnType);
        Assert.True(operation.IsNullable);
    }
}
