using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608180001_AddDailyRouteOptimization")]
public sealed class AddDailyRouteOptimization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE daily_route_optimization_results (
            "Id" uuid NOT NULL, "RouteImportId" uuid NOT NULL, "JobExecutionId" uuid NOT NULL,
            "Weekday" character varying(16) NOT NULL, "Status" character varying(32) NOT NULL,
            "Reason" character varying(1024), "CurrentDistanceMeters" numeric(18,3) NOT NULL,
            "CurrentDurationSeconds" numeric(18,3) NOT NULL, "ProposedDistanceMeters" numeric(18,3) NOT NULL,
            "ProposedDurationSeconds" numeric(18,3) NOT NULL, "CurrentVehicleCount" integer NOT NULL,
            "ProposedVehicleCount" integer NOT NULL, "AdditionalVehicleCount" integer NOT NULL,
            "AdditionalCapacityKg" numeric(18,3) NOT NULL, "TotalWeightKg" numeric(18,3) NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_daily_route_optimization_results" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_daily_route_optimization_results_import" FOREIGN KEY ("RouteImportId") REFERENCES imports ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_daily_route_optimization_results_job" FOREIGN KEY ("JobExecutionId") REFERENCES job_executions ("Id") ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX "IX_daily_route_optimization_results_Import_Weekday"
            ON daily_route_optimization_results ("RouteImportId", "Weekday");

        CREATE TABLE daily_route_optimization_vehicles (
            "Id" uuid NOT NULL, "ResultId" uuid NOT NULL, "VehicleTypeId" uuid NOT NULL,
            "SourceRouteId" uuid, "Sequence" integer NOT NULL, "IsAdditional" boolean NOT NULL,
            "IsIdle" boolean NOT NULL, "CapacityKg" numeric(18,3) NOT NULL,
            "LoadKg" numeric(18,3) NOT NULL, "Occupancy" numeric(12,6) NOT NULL,
            "DistanceMeters" numeric(18,3) NOT NULL, "DurationSeconds" numeric(18,3) NOT NULL,
            CONSTRAINT "PK_daily_route_optimization_vehicles" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_daily_route_optimization_vehicles_result" FOREIGN KEY ("ResultId") REFERENCES daily_route_optimization_results ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_daily_route_optimization_vehicles_type" FOREIGN KEY ("VehicleTypeId") REFERENCES vehicle_types ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_daily_route_optimization_vehicles_route" FOREIGN KEY ("SourceRouteId") REFERENCES routes ("Id") ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX "IX_daily_route_optimization_vehicles_Result_Sequence"
            ON daily_route_optimization_vehicles ("ResultId", "Sequence");

        CREATE TABLE daily_route_optimization_stops (
            "Id" uuid NOT NULL, "VehicleId" uuid NOT NULL, "MunicipalityId" uuid NOT NULL,
            "Sequence" integer NOT NULL, "WeightKg" numeric(18,3) NOT NULL,
            "DistanceFromPreviousMeters" numeric(18,3) NOT NULL,
            "DurationFromPreviousSeconds" numeric(18,3) NOT NULL,
            CONSTRAINT "PK_daily_route_optimization_stops" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_daily_route_optimization_stops_vehicle" FOREIGN KEY ("VehicleId") REFERENCES daily_route_optimization_vehicles ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_daily_route_optimization_stops_municipality" FOREIGN KEY ("MunicipalityId") REFERENCES municipalities ("Id") ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX "IX_daily_route_optimization_stops_Vehicle_Sequence"
            ON daily_route_optimization_stops ("VehicleId", "Sequence");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS daily_route_optimization_stops;
        DROP TABLE IF EXISTS daily_route_optimization_vehicles;
        DROP TABLE IF EXISTS daily_route_optimization_results;
        """);
}
