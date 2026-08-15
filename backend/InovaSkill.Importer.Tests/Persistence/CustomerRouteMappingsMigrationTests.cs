using System.Reflection;
using InovaSkill.Importer.Infrastructure.Persistence.Migrations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class CustomerRouteMappingsMigrationTests
{
    [Fact]
    public void Migration_DefinesTraceabilityAndAccessIndexes()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "backend",
            "InovaSkill.Importer.Infrastructure", "Persistence", "Migrations",
            "202608150002_AddCustomerRouteMappings.cs"));
        Assert.Contains("customer_route_mappings", source);
        Assert.Contains("ImportId_SourceRowNumber_SheetName", source);
        Assert.Contains("ImportId_Weekday_NormalizedRouteName", source);
        Assert.Contains("ImportId_CustomerId", source);
        Assert.NotNull(typeof(AddCustomerRouteMappings).GetCustomAttribute<Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute>());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
