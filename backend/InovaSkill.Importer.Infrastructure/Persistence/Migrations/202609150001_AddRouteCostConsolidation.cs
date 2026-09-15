using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609150001_AddRouteCostConsolidation")]
public sealed class AddRouteCostConsolidation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE vehicle_types
            ADD COLUMN "AxleCount" integer NULL,
            ADD COLUMN "MinimumFuelEfficiencyKmPerLiter" numeric(8,3) NULL,
            ADD COLUMN "MaximumFuelEfficiencyKmPerLiter" numeric(8,3) NULL;

        UPDATE vehicle_types SET
            "AxleCount" = 2,
            "MinimumFuelEfficiencyKmPerLiter" = 5.500,
            "MaximumFuelEfficiencyKmPerLiter" = 7.000
        WHERE upper("Name") LIKE '%ACCELO%' OR upper("Name") LIKE '%ACELO%';
        UPDATE vehicle_types SET
            "AxleCount" = 2,
            "MinimumFuelEfficiencyKmPerLiter" = 3.800,
            "MaximumFuelEfficiencyKmPerLiter" = 4.500
        WHERE upper("Name") LIKE '%TOCO%';
        UPDATE vehicle_types SET
            "AxleCount" = 3,
            "MinimumFuelEfficiencyKmPerLiter" = 3.200,
            "MaximumFuelEfficiencyKmPerLiter" = 4.000
        WHERE upper("Name") LIKE '%TRUCK%';

        ALTER TABLE vehicle_types ADD CONSTRAINT "CK_vehicle_types_axle_count"
            CHECK (("AxleCount" IS NULL AND "MinimumFuelEfficiencyKmPerLiter" IS NULL AND "MaximumFuelEfficiencyKmPerLiter" IS NULL)
                OR ("AxleCount" IS NOT NULL AND "AxleCount" BETWEEN 2 AND 9 AND "MinimumFuelEfficiencyKmPerLiter" IS NOT NULL AND "MaximumFuelEfficiencyKmPerLiter" IS NOT NULL));
        ALTER TABLE vehicle_types ADD CONSTRAINT "CK_vehicle_types_fuel_efficiency"
            CHECK (("AxleCount" IS NULL AND "MinimumFuelEfficiencyKmPerLiter" IS NULL AND "MaximumFuelEfficiencyKmPerLiter" IS NULL)
                OR ("AxleCount" IS NOT NULL AND "MinimumFuelEfficiencyKmPerLiter" IS NOT NULL AND "MaximumFuelEfficiencyKmPerLiter" IS NOT NULL
                    AND "MinimumFuelEfficiencyKmPerLiter" > 0
                    AND "MaximumFuelEfficiencyKmPerLiter" >= "MinimumFuelEfficiencyKmPerLiter"));

        CREATE TABLE route_cost_snapshots (
            "Id" uuid PRIMARY KEY,
            "RouteImportId" uuid NOT NULL REFERENCES imports("Id") ON DELETE CASCADE,
            "JobExecutionId" uuid NOT NULL REFERENCES job_executions("Id") ON DELETE RESTRICT,
            "InputFingerprint" varchar(64) NOT NULL,
            "DieselPricePerLiter" numeric(10,3) NULL,
            "TollCatalogVersion" varchar(64) NOT NULL,
            "TollEffectiveFrom" date NULL,
            "CalculatedAt" timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX "IX_route_cost_snapshots_RouteImportId" ON route_cost_snapshots ("RouteImportId");
        CREATE INDEX "IX_route_cost_snapshots_RouteImportId_InputFingerprint" ON route_cost_snapshots ("RouteImportId", "InputFingerprint");

        CREATE TABLE route_cost_items (
            "Id" uuid PRIMARY KEY,
            "SnapshotId" uuid NOT NULL REFERENCES route_cost_snapshots("Id") ON DELETE CASCADE,
            "Scenario" varchar(16) NOT NULL,
            "Weekday" varchar(16) NOT NULL,
            "RouteId" uuid NULL REFERENCES routes("Id") ON DELETE RESTRICT,
            "OptimizationResultId" uuid NULL REFERENCES daily_route_optimization_results("Id") ON DELETE RESTRICT,
            "OptimizationVehicleId" uuid NULL REFERENCES daily_route_optimization_vehicles("Id") ON DELETE RESTRICT,
            "VehicleTypeId" uuid NULL REFERENCES vehicle_types("Id") ON DELETE RESTRICT,
            "Label" varchar(256) NOT NULL,
            "PathBasis" varchar(32) NOT NULL,
            "IsAvailable" boolean NOT NULL,
            "UnavailableReason" varchar(1024) NULL,
            "DistanceMeters" numeric(18,3) NULL,
            "DurationSeconds" numeric(18,3) NULL,
            "MinimumFuelLiters" numeric(18,3) NULL,
            "MaximumFuelLiters" numeric(18,3) NULL,
            "MinimumFuelCost" numeric(18,2) NULL,
            "MaximumFuelCost" numeric(18,2) NULL,
            "TollCost" numeric(18,2) NOT NULL,
            "TollPassages" integer NOT NULL,
            "MinimumTotalCost" numeric(18,2) NULL,
            "MaximumTotalCost" numeric(18,2) NULL
        );
        CREATE INDEX "IX_route_cost_items_SnapshotId_Scenario_Weekday" ON route_cost_items ("SnapshotId", "Scenario", "Weekday");
        CREATE UNIQUE INDEX "IX_route_cost_items_SnapshotId_RouteId" ON route_cost_items ("SnapshotId", "RouteId") WHERE "RouteId" IS NOT NULL;
        CREATE UNIQUE INDEX "IX_route_cost_items_SnapshotId_OptimizationVehicleId" ON route_cost_items ("SnapshotId", "OptimizationVehicleId") WHERE "OptimizationVehicleId" IS NOT NULL;
        CREATE INDEX "IX_route_cost_items_OptimizationResultId" ON route_cost_items ("OptimizationResultId");

        CREATE TABLE route_cost_toll_passages (
            "Id" uuid PRIMARY KEY,
            "RouteCostItemId" uuid NOT NULL REFERENCES route_cost_items("Id") ON DELETE CASCADE,
            "TollPlazaCode" varchar(64) NOT NULL,
            "TollPlazaName" varchar(160) NOT NULL,
            "OperatorName" varchar(160) NOT NULL,
            "Highway" varchar(32) NOT NULL,
            "Kilometer" numeric(8,2) NOT NULL,
            "AxleCount" integer NOT NULL,
            "Passages" integer NOT NULL,
            "AutomaticUnitTariff" numeric(18,2) NOT NULL,
            "TotalCost" numeric(18,2) NOT NULL
        );
        CREATE UNIQUE INDEX "IX_route_cost_toll_passages_RouteCostItemId_TollPlazaCode"
            ON route_cost_toll_passages ("RouteCostItemId", "TollPlazaCode");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS route_cost_toll_passages;
        DROP TABLE IF EXISTS route_cost_items;
        DROP TABLE IF EXISTS route_cost_snapshots;
        ALTER TABLE vehicle_types DROP CONSTRAINT IF EXISTS "CK_vehicle_types_axle_count";
        ALTER TABLE vehicle_types DROP CONSTRAINT IF EXISTS "CK_vehicle_types_fuel_efficiency";
        ALTER TABLE vehicle_types DROP COLUMN IF EXISTS "AxleCount";
        ALTER TABLE vehicle_types DROP COLUMN IF EXISTS "MinimumFuelEfficiencyKmPerLiter";
        ALTER TABLE vehicle_types DROP COLUMN IF EXISTS "MaximumFuelEfficiencyKmPerLiter";
        """);
}
