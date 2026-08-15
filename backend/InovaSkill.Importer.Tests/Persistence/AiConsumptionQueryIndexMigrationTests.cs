using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AiConsumptionQueryIndexMigrationTests
{
    [Fact]
    public void Up_CreatesTrigramIndexesForPartialUserSearch()
    {
        var migration = new AddAiConsumptionReportIndex();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("IX_app_users_Name_trgm", sql);
        Assert.Contains("IX_app_users_Email_trgm", sql);
        Assert.Contains("gin_trgm_ops", sql);
        Assert.Contains(migration.UpOperations.OfType<CreateIndexOperation>(),
            index => index.Table == "ai_provider_calls" && index.Columns.SequenceEqual(["CreatedAt"]));
    }
}
