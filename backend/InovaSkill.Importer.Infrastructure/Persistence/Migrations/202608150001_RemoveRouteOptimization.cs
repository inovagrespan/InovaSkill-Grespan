using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608150001_RemoveRouteOptimization")]
public sealed class RemoveRouteOptimization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM job_schedules
            WHERE "JobType" = 'ROUTE_OPTIMIZATION';

            DROP TABLE IF EXISTS route_optimization_scenarios;
            DROP TABLE IF EXISTS route_optimization_runs;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE route_optimization_runs (
                "Id" uuid PRIMARY KEY,
                "Scope" varchar(32) NOT NULL,
                "TargetRouteId" uuid NULL REFERENCES routes("Id") ON DELETE SET NULL,
                "ReferenceDate" date NOT NULL,
                "RequestedByUserId" bigint NOT NULL,
                "RequestedFrom" varchar(32) NOT NULL,
                "Status" varchar(32) NOT NULL,
                "Priority" integer NOT NULL,
                "AlgorithmVersion" varchar(64) NOT NULL,
                "RulesVersion" varchar(64) NOT NULL,
                "InputHash" varchar(64) NULL,
                "Confidence" varchar(32) NOT NULL,
                "ProgressStage" varchar(32) NOT NULL,
                "ProgressPercentage" numeric(5,2) NULL,
                "SnapshotImportId" uuid NULL REFERENCES imports("Id") ON DELETE SET NULL,
                "SnapshotImportVersion" bigint NULL,
                "CreatedAt" timestamptz NOT NULL,
                "StartedAt" timestamptz NULL,
                "CompletedAt" timestamptz NULL,
                "CancelledAt" timestamptz NULL,
                "ErrorCode" varchar(64) NULL,
                "ErrorMessage" varchar(1024) NULL
            );

            CREATE TABLE route_optimization_scenarios (
                "Id" uuid PRIMARY KEY,
                "RunId" uuid NOT NULL REFERENCES route_optimization_runs("Id") ON DELETE CASCADE,
                "Rank" integer NOT NULL,
                "Score" numeric(12,4) NOT NULL,
                "ActionType" varchar(32) NOT NULL,
                "IsRecommended" boolean NOT NULL,
                "Confidence" varchar(32) NOT NULL,
                "EstimatedDistanceChangeKm" numeric(12,2) NULL,
                "CurrentMetricsJson" jsonb NOT NULL,
                "ProposedMetricsJson" jsonb NOT NULL,
                "WarningsJson" jsonb NOT NULL,
                "ReasonsJson" jsonb NOT NULL,
                "CityReallocationsJson" jsonb NOT NULL,
                "TruckChangeJson" jsonb NULL,
                "CreatedAt" timestamptz NOT NULL
            );

            CREATE INDEX "IX_route_optimization_runs_Scope_ReferenceDate_Status"
                ON route_optimization_runs ("Scope", "ReferenceDate", "Status");
            CREATE INDEX "IX_route_optimization_runs_TargetRouteId_ReferenceDate_CreatedAt"
                ON route_optimization_runs ("TargetRouteId", "ReferenceDate", "CreatedAt");
            CREATE INDEX "IX_route_optimization_runs_InputHash"
                ON route_optimization_runs ("InputHash");
            CREATE UNIQUE INDEX "IX_route_optimization_scenarios_RunId_Rank"
                ON route_optimization_scenarios ("RunId", "Rank");
            """);
    }
}
