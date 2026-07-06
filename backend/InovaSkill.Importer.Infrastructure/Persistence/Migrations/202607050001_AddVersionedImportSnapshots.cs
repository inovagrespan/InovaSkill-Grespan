using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607050001_AddVersionedImportSnapshots")]
public sealed class AddVersionedImportSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE data_sources
                ADD COLUMN "ProcessorKey" varchar(128) NOT NULL DEFAULT '',
                ADD COLUMN "ImportMode" varchar(16) NOT NULL DEFAULT 'Append',
                ADD COLUMN "NextImportVersion" bigint NOT NULL DEFAULT 1,
                ADD COLUMN "CurrentImportId" uuid NULL,
                ADD COLUMN "LastSuccessfulImportId" uuid NULL,
                ADD COLUMN "StateUpdatedAt" timestamptz NULL;

            UPDATE data_sources
            SET "ProcessorKey" = CASE
                    WHEN "Code" = 'ROUTES_BY_CITY' THEN 'logistics-routes'
                    ELSE "Code"
                END,
                "ImportMode" = CASE
                    WHEN "Code" = 'ROUTES_BY_CITY' THEN 'Snapshot'
                    ELSE 'Append'
                END;

            ALTER TABLE imports ADD COLUMN "Version" bigint;

            WITH numbered_imports AS (
                SELECT "Id",
                       row_number() OVER (
                           PARTITION BY "DataSourceId"
                           ORDER BY "CreatedAt", "Id") AS version
                FROM imports
            )
            UPDATE imports
            SET "Version" = numbered_imports.version
            FROM numbered_imports
            WHERE imports."Id" = numbered_imports."Id";

            ALTER TABLE imports ALTER COLUMN "Version" SET NOT NULL;

            UPDATE data_sources AS source
            SET "NextImportVersion" = version_state.next_version
            FROM (
                SELECT "DataSourceId", COALESCE(MAX("Version"), 0) + 1 AS next_version
                FROM imports
                GROUP BY "DataSourceId"
            ) AS version_state
            WHERE source."Id" = version_state."DataSourceId";

            WITH latest_completed AS (
                SELECT DISTINCT ON ("DataSourceId")
                       "DataSourceId", "Id"
                FROM imports
                WHERE "Status" = 'Completed'
                ORDER BY "DataSourceId", "Version" DESC
            )
            UPDATE data_sources AS source
            SET "CurrentImportId" = latest_completed."Id",
                "LastSuccessfulImportId" = latest_completed."Id",
                "StateUpdatedAt" = now()
            FROM latest_completed
            WHERE source."Id" = latest_completed."DataSourceId"
              AND source."ImportMode" = 'Snapshot';

            CREATE UNIQUE INDEX "IX_imports_DataSourceId_Version"
                ON imports ("DataSourceId", "Version");

            ALTER TABLE data_sources
                ADD CONSTRAINT "FK_data_sources_imports_CurrentImportId"
                    FOREIGN KEY ("CurrentImportId") REFERENCES imports("Id") ON DELETE RESTRICT,
                ADD CONSTRAINT "FK_data_sources_imports_LastSuccessfulImportId"
                    FOREIGN KEY ("LastSuccessfulImportId") REFERENCES imports("Id") ON DELETE RESTRICT;

            ALTER TABLE vehicle_types
                ALTER COLUMN "CapacityKg" DROP NOT NULL,
                ADD COLUMN "CapacityVolumeM3" numeric(12,3) NULL,
                ADD COLUMN "CapacityPallets" integer NULL;

            ALTER TABLE routes
                ADD COLUMN "TotalWeightKg" numeric(18,3) NOT NULL DEFAULT 0,
                ADD COLUMN "TotalVolumeM3" numeric(18,3) NULL,
                ADD COLUMN "TotalPallets" integer NULL,
                ADD COLUMN "WeightOccupancy" numeric(12,6) NULL,
                ADD COLUMN "VolumeOccupancy" numeric(12,6) NULL,
                ADD COLUMN "PalletOccupancy" numeric(12,6) NULL,
                ADD COLUMN "OverallOccupancy" numeric(12,6) NULL,
                ADD COLUMN "OccupancyStatus" varchar(32) NOT NULL DEFAULT 'MissingCapacity';

            UPDATE vehicle_types
            SET "CapacityKg" = CASE lower("Name")
                WHEN 'truck' THEN 10300
                WHEN 'toco' THEN 7700
                WHEN 'acelo' THEN 3300
                ELSE "CapacityKg"
            END
            WHERE "CapacityKg" IS NULL;

            WITH route_loads AS (
                SELECT route."Id",
                       COALESCE(SUM(entry."AveragePerDay"), 0) AS total_weight
                FROM routes AS route
                LEFT JOIN route_entries AS entry ON entry."RouteId" = route."Id"
                GROUP BY route."Id"
            )
            UPDATE routes AS route
            SET "TotalWeightKg" = route_loads.total_weight,
                "WeightOccupancy" = CASE
                    WHEN vehicle."CapacityKg" > 0
                    THEN route_loads.total_weight / vehicle."CapacityKg"
                    ELSE NULL
                END,
                "OverallOccupancy" = CASE
                    WHEN vehicle."CapacityKg" > 0
                    THEN route_loads.total_weight / vehicle."CapacityKg"
                    ELSE NULL
                END,
                "OccupancyStatus" = CASE
                    WHEN vehicle."CapacityKg" > 0 THEN 'Calculated'
                    ELSE 'MissingCapacity'
                END
            FROM route_loads, vehicle_types AS vehicle
            WHERE route."Id" = route_loads."Id"
              AND route."VehicleTypeId" = vehicle."Id";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE data_sources
                DROP CONSTRAINT IF EXISTS "FK_data_sources_imports_CurrentImportId",
                DROP CONSTRAINT IF EXISTS "FK_data_sources_imports_LastSuccessfulImportId";
            DROP INDEX IF EXISTS "IX_imports_DataSourceId_Version";
            ALTER TABLE routes
                DROP COLUMN "TotalWeightKg",
                DROP COLUMN "TotalVolumeM3",
                DROP COLUMN "TotalPallets",
                DROP COLUMN "WeightOccupancy",
                DROP COLUMN "VolumeOccupancy",
                DROP COLUMN "PalletOccupancy",
                DROP COLUMN "OverallOccupancy",
                DROP COLUMN "OccupancyStatus";
            ALTER TABLE vehicle_types
                ALTER COLUMN "CapacityKg" SET NOT NULL,
                DROP COLUMN "CapacityVolumeM3",
                DROP COLUMN "CapacityPallets";
            ALTER TABLE imports DROP COLUMN "Version";
            ALTER TABLE data_sources
                DROP COLUMN "ProcessorKey",
                DROP COLUMN "ImportMode",
                DROP COLUMN "NextImportVersion",
                DROP COLUMN "CurrentImportId",
                DROP COLUMN "LastSuccessfulImportId",
                DROP COLUMN "StateUpdatedAt";
            """);
    }
}
