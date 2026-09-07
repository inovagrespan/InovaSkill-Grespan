using InovaSkill.Importer.Infrastructure.Persistence.Migrations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddDailyRouteOptimizationMigrationTests
{
    [Fact]
    public void MigrationDefinesResultUniquenessAndOrderedChildren()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "backend/InovaSkill.Importer.Infrastructure/Persistence/Migrations/202608180001_AddDailyRouteOptimization.cs"));
        Assert.Contains("daily_route_optimization_results", source);
        Assert.Contains("RouteImportId\", \"Weekday", source);
        Assert.Contains("ResultId\", \"Sequence", source);
        Assert.Contains("VehicleId\", \"Sequence", source);
        Assert.Contains("ON DELETE CASCADE", source);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "backend")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
