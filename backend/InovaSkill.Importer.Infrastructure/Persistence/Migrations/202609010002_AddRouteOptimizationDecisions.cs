using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609010002_AddRouteOptimizationDecisions")]
public sealed class AddRouteOptimizationDecisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE route_optimization_decisions (
            "JobExecutionId" uuid PRIMARY KEY REFERENCES job_executions("Id") ON DELETE CASCADE,
            "Status" varchar(16) NOT NULL,
            "DecidedByUserId" bigint NULL REFERENCES app_users("Id") ON DELETE RESTRICT,
            "DecidedAt" timestamptz NULL,
            "Justification" varchar(1000) NULL,
            "CreatedAt" timestamptz NOT NULL
        );
        CREATE INDEX "IX_route_optimization_decisions_Status_DecidedAt"
            ON route_optimization_decisions ("Status", "DecidedAt");
        CREATE INDEX "IX_route_optimization_decisions_DecidedByUserId"
            ON route_optimization_decisions ("DecidedByUserId");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS route_optimization_decisions;");
}
