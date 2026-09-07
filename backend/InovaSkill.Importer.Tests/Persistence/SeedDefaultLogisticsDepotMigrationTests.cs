using InovaSkill.Importer.Infrastructure.Persistence.Migrations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class SeedDefaultLogisticsDepotMigrationTests
{
    [Fact]
    public void MigrationSeedsHeadquartersOnlyWhenDepotDoesNotExist()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "backend/InovaSkill.Importer.Infrastructure/Persistence/Migrations/202608180002_SeedDefaultLogisticsDepot.cs"));

        Assert.Contains("Grespan Matriz", source);
        Assert.Contains("Avenida República, 7000", source);
        Assert.Contains("-22.213890", source);
        Assert.Contains("-49.945830", source);
        Assert.Contains("WHERE NOT EXISTS (SELECT 1 FROM logistics_depots)", source);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "backend")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
