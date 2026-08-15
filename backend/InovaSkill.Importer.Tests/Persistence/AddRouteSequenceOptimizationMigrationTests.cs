using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddRouteSequenceOptimizationMigrationTests
{
    [Fact]
    public void Up_AddsRequiredJsonColumnWithoutCreatingUnnecessaryIndex()
    {
        var operations = new AddRouteSequenceOptimization().UpOperations;

        var column = Assert.IsType<AddColumnOperation>(Assert.Single(operations));
        Assert.Equal("RouteSequencesJson", column.Name);
        Assert.Equal("route_optimization_scenarios", column.Table);
        Assert.False(column.IsNullable);
        Assert.Equal("jsonb", column.ColumnType);
        Assert.DoesNotContain(operations, operation => operation is CreateIndexOperation);
    }
}
