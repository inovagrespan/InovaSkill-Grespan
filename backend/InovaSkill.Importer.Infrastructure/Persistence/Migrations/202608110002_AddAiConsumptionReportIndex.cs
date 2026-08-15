using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608110002_AddAiConsumptionReportIndex")]
public sealed class AddAiConsumptionReportIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_ai_provider_calls_CreatedAt",
            table: "ai_provider_calls",
            column: "CreatedAt");
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS pg_trgm;
            CREATE INDEX IF NOT EXISTS "IX_app_users_Name_trgm"
                ON app_users USING gin ("Name" gin_trgm_ops);
            CREATE INDEX IF NOT EXISTS "IX_app_users_Email_trgm"
                ON app_users USING gin ("Email" gin_trgm_ops);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_app_users_Name_trgm";
            DROP INDEX IF EXISTS "IX_app_users_Email_trgm";
            """);
        migrationBuilder.DropIndex(
            name: "IX_ai_provider_calls_CreatedAt",
            table: "ai_provider_calls");
    }
}
