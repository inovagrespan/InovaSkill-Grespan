using InovaSkill.Importer.Infrastructure.Persistence.Migrations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class RemoveCustomerDeliveryProfilesMigrationTests
{
    [Fact]
    public void MigrationRemovesDeliveryProfilesAndCanRestoreSchemaOnRollback()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "backend/InovaSkill.Importer.Infrastructure/Persistence/Migrations/202608210001_RemoveCustomerDeliveryProfiles.cs"));
        Assert.Contains("DropTable(name: \"customer_delivery_profiles\")", source);
        Assert.Contains("CREATE TABLE customer_delivery_profiles", source);
        Assert.NotNull(typeof(RemoveCustomerDeliveryProfiles)
            .GetCustomAttributes(false).Single(attribute => attribute.GetType().Name == "MigrationAttribute"));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "backend")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
