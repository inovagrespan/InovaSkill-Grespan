using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609050001_AddRouteOptimizationRemediation")]
public class AddRouteOptimizationRemediation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE imports ADD COLUMN "DerivedFromImportId" uuid;
        ALTER TABLE imports ADD COLUMN "CreatedByUserId" bigint;
        ALTER TABLE imports ADD CONSTRAINT "FK_imports_DerivedFromImportId"
            FOREIGN KEY ("DerivedFromImportId") REFERENCES imports ("Id") ON DELETE RESTRICT;
        ALTER TABLE imports ADD CONSTRAINT "FK_imports_CreatedByUserId"
            FOREIGN KEY ("CreatedByUserId") REFERENCES app_users ("Id") ON DELETE RESTRICT;
        CREATE INDEX "IX_imports_DerivedFromImportId" ON imports ("DerivedFromImportId");

        ALTER TABLE routes ADD COLUMN "SourceSheetName" character varying(128);
        ALTER TABLE routes ADD COLUMN "SourceHeaderRowNumber" integer;
        ALTER TABLE routes ADD COLUMN "VehicleCapacityKgSnapshot" numeric(18,3) NOT NULL DEFAULT 0;
        UPDATE routes r SET "VehicleCapacityKgSnapshot" = COALESCE(vt."CapacityKg", 0)
        FROM vehicle_types vt WHERE vt."Id" = r."VehicleTypeId";

        ALTER TABLE route_entries ADD COLUMN "SourceRowNumber" integer;
        ALTER TABLE route_entries ADD COLUMN "IsExcludedFromOptimization" boolean NOT NULL DEFAULT FALSE;

        ALTER TABLE job_executions ADD COLUMN "ParentJobExecutionId" uuid;
        ALTER TABLE job_executions ADD CONSTRAINT "FK_job_executions_ParentJobExecutionId"
            FOREIGN KEY ("ParentJobExecutionId") REFERENCES job_executions ("Id") ON DELETE SET NULL;
        CREATE INDEX "IX_job_executions_ParentJobExecutionId" ON job_executions ("ParentJobExecutionId");

        ALTER TABLE daily_route_optimization_results ADD COLUMN "InheritedFromResultId" uuid;
        ALTER TABLE daily_route_optimization_results ADD CONSTRAINT "FK_daily_route_results_InheritedFromResultId"
            FOREIGN KEY ("InheritedFromResultId") REFERENCES daily_route_optimization_results ("Id") ON DELETE SET NULL;
        CREATE INDEX "IX_daily_route_optimization_results_InheritedFromResultId"
            ON daily_route_optimization_results ("InheritedFromResultId");

        CREATE TABLE municipality_aliases (
            "Id" uuid NOT NULL,
            "DataSourceId" uuid NOT NULL,
            "Alias" character varying(256) NOT NULL,
            "NormalizedAlias" character varying(256) NOT NULL,
            "MunicipalityId" uuid NOT NULL,
            "CreatedByUserId" bigint NOT NULL,
            "UpdatedByUserId" bigint NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_municipality_aliases" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_municipality_aliases_source" FOREIGN KEY ("DataSourceId") REFERENCES data_sources ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_municipality_aliases_municipality" FOREIGN KEY ("MunicipalityId") REFERENCES municipalities ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_municipality_aliases_created_user" FOREIGN KEY ("CreatedByUserId") REFERENCES app_users ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_municipality_aliases_updated_user" FOREIGN KEY ("UpdatedByUserId") REFERENCES app_users ("Id") ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX "IX_municipality_aliases_Source_NormalizedAlias"
            ON municipality_aliases ("DataSourceId", "NormalizedAlias");
        CREATE INDEX "IX_municipality_aliases_MunicipalityId" ON municipality_aliases ("MunicipalityId");

        CREATE TABLE route_import_corrections (
            "Id" uuid NOT NULL,
            "DerivedImportId" uuid NOT NULL,
            "Kind" character varying(64) NOT NULL,
            "SourceRouteId" uuid,
            "SourceRouteEntryId" uuid,
            "MunicipalityId" uuid,
            "OriginalMunicipalityId" uuid,
            "VehicleTypeId" uuid,
            "SourceLabel" character varying(256),
            "OriginalWeightKg" numeric(18,3),
            "CorrectedWeightKg" numeric(18,3),
            "OriginalCapacityKg" numeric(18,3),
            "CorrectedCapacityKg" numeric(18,3),
            "OriginalLatitude" numeric(9,6),
            "OriginalLongitude" numeric(9,6),
            "CorrectedLatitude" numeric(9,6),
            "CorrectedLongitude" numeric(9,6),
            "ExcludeFromOptimization" boolean NOT NULL,
            "RequestedByUserId" bigint NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_route_import_corrections" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_route_import_corrections_import" FOREIGN KEY ("DerivedImportId") REFERENCES imports ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_route_import_corrections_user" FOREIGN KEY ("RequestedByUserId") REFERENCES app_users ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_route_import_corrections_route" FOREIGN KEY ("SourceRouteId") REFERENCES routes ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_route_import_corrections_entry" FOREIGN KEY ("SourceRouteEntryId") REFERENCES route_entries ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_route_import_corrections_municipality" FOREIGN KEY ("MunicipalityId") REFERENCES municipalities ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_route_import_corrections_vehicle_type" FOREIGN KEY ("VehicleTypeId") REFERENCES vehicle_types ("Id") ON DELETE RESTRICT
        );
        CREATE INDEX "IX_route_import_corrections_Import_Kind" ON route_import_corrections ("DerivedImportId", "Kind");
        CREATE INDEX "IX_route_import_corrections_SourceRouteEntryId" ON route_import_corrections ("SourceRouteEntryId");

        CREATE TABLE route_import_affected_weekdays (
            "ImportId" uuid NOT NULL,
            "Weekday" character varying(16) NOT NULL,
            CONSTRAINT "PK_route_import_affected_weekdays" PRIMARY KEY ("ImportId", "Weekday"),
            CONSTRAINT "FK_route_import_affected_weekdays_import" FOREIGN KEY ("ImportId") REFERENCES imports ("Id") ON DELETE CASCADE
        );

        CREATE TABLE daily_route_optimization_issues (
            "Id" uuid NOT NULL,
            "ResultId" uuid NOT NULL,
            "Code" character varying(64) NOT NULL,
            "RouteId" uuid,
            "RouteEntryId" uuid,
            "MunicipalityId" uuid,
            "VehicleTypeId" uuid,
            "Message" character varying(1024) NOT NULL,
            "CurrentValue" character varying(256),
            "CanResolve" boolean NOT NULL,
            CONSTRAINT "PK_daily_route_optimization_issues" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_daily_route_optimization_issues_result" FOREIGN KEY ("ResultId") REFERENCES daily_route_optimization_results ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_daily_route_optimization_issues_route" FOREIGN KEY ("RouteId") REFERENCES routes ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_daily_route_optimization_issues_entry" FOREIGN KEY ("RouteEntryId") REFERENCES route_entries ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_daily_route_optimization_issues_municipality" FOREIGN KEY ("MunicipalityId") REFERENCES municipalities ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_daily_route_optimization_issues_vehicle_type" FOREIGN KEY ("VehicleTypeId") REFERENCES vehicle_types ("Id") ON DELETE RESTRICT
        );
        CREATE INDEX "IX_daily_route_optimization_issues_Result_Code" ON daily_route_optimization_issues ("ResultId", "Code");
        CREATE INDEX "IX_daily_route_optimization_issues_RouteEntryId" ON daily_route_optimization_issues ("RouteEntryId");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS daily_route_optimization_issues;
        DROP TABLE IF EXISTS route_import_affected_weekdays;
        DROP TABLE IF EXISTS route_import_corrections;
        DROP TABLE IF EXISTS municipality_aliases;
        DROP INDEX IF EXISTS "IX_daily_route_optimization_results_InheritedFromResultId";
        ALTER TABLE daily_route_optimization_results DROP CONSTRAINT IF EXISTS "FK_daily_route_results_InheritedFromResultId";
        ALTER TABLE daily_route_optimization_results DROP COLUMN IF EXISTS "InheritedFromResultId";
        DROP INDEX IF EXISTS "IX_job_executions_ParentJobExecutionId";
        ALTER TABLE job_executions DROP CONSTRAINT IF EXISTS "FK_job_executions_ParentJobExecutionId";
        ALTER TABLE job_executions DROP COLUMN IF EXISTS "ParentJobExecutionId";
        ALTER TABLE route_entries DROP COLUMN IF EXISTS "IsExcludedFromOptimization";
        ALTER TABLE route_entries DROP COLUMN IF EXISTS "SourceRowNumber";
        ALTER TABLE routes DROP COLUMN IF EXISTS "VehicleCapacityKgSnapshot";
        ALTER TABLE routes DROP COLUMN IF EXISTS "SourceHeaderRowNumber";
        ALTER TABLE routes DROP COLUMN IF EXISTS "SourceSheetName";
        DROP INDEX IF EXISTS "IX_imports_DerivedFromImportId";
        ALTER TABLE imports DROP CONSTRAINT IF EXISTS "FK_imports_DerivedFromImportId";
        ALTER TABLE imports DROP CONSTRAINT IF EXISTS "FK_imports_CreatedByUserId";
        ALTER TABLE imports DROP COLUMN IF EXISTS "CreatedByUserId";
        ALTER TABLE imports DROP COLUMN IF EXISTS "DerivedFromImportId";
        """);
}
