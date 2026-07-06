using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class MunicipalitySearchIndexMigrationTests
{
    [Fact]
    public void Up_CreatesTrigramIndexForPartialMunicipalitySearch()
    {
        var sql = Assert.Single(new AddMunicipalitySearchIndex().UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("IX_municipalities_NormalizedName_trgm", sql);
        Assert.Contains("gin_trgm_ops", sql);
        Assert.Contains("\"NormalizedName\"", sql);
    }
}
