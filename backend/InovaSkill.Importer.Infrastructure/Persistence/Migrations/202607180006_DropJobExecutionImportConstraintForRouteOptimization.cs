using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607180006_DropJobExecutionImportConstraintForRouteOptimization")]
public sealed class DropJobExecutionImportConstraintForRouteOptimization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE job_executions
                DROP CONSTRAINT IF EXISTS "FK_job_executions_imports_RelatedEntityId";

            ALTER TABLE job_executions
                DROP CONSTRAINT IF EXISTS "job_executions_RelatedEntityId_fkey";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE job_executions
                ADD CONSTRAINT "job_executions_RelatedEntityId_fkey"
                FOREIGN KEY ("RelatedEntityId") REFERENCES imports("Id") ON DELETE CASCADE;
            """);
    }
}
