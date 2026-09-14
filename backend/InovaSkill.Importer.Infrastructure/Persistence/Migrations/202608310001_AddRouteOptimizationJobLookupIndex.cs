using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608310001_AddRouteOptimizationJobLookupIndex")]
public sealed class AddRouteOptimizationJobLookupIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateIndex(
        name: "IX_job_executions_JobType_RelatedEntityId_CreatedAt",
        table: "job_executions",
        columns: ["JobType", "RelatedEntityId", "CreatedAt"]);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropIndex(
        name: "IX_job_executions_JobType_RelatedEntityId_CreatedAt",
        table: "job_executions");
}
