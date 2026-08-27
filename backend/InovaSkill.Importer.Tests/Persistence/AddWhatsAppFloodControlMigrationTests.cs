using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AddWhatsAppFloodControlMigrationTests
{
    [Fact]
    public void Migration_AddsPersistentCooldownWithoutCreatingAnotherMessageIndex()
    {
        var migration = new AddWhatsAppFloodControl();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("FloodBlockedUntil", sql);
        Assert.DoesNotContain("CREATE INDEX", sql, StringComparison.OrdinalIgnoreCase);
    }
}
