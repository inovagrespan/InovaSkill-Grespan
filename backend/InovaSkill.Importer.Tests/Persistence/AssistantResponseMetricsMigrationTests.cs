using InovaSkill.Importer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InovaSkill.Importer.Tests.Persistence;

public sealed class AssistantResponseMetricsMigrationTests
{
    [Fact]
    public void Up_AddsAuditFieldsLinksBackfillAndReportIndex()
    {
        var migration = new AddAssistantResponseMetrics();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("QuestionMessageId", sql);
        Assert.Contains("ResponseMessageId", sql);
        Assert.Contains("DurationMilliseconds", sql);
        Assert.Contains("EXTRACT(EPOCH", sql);
        Assert.Contains("IX_ai_response_executions_CreatedAt", sql);
    }
}
