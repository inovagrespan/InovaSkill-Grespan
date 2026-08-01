using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608010003_PreserveRouteOvercapacity")]
public sealed class PreserveRouteOvercapacity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE routes AS route
        SET "WeightOccupancy" = CASE WHEN vehicle."CapacityKg" > 0 THEN GREATEST(0, route."TotalWeightKg" / vehicle."CapacityKg") ELSE NULL END,
            "VolumeOccupancy" = CASE WHEN vehicle."CapacityVolumeM3" > 0 AND route."TotalVolumeM3" IS NOT NULL THEN GREATEST(0, route."TotalVolumeM3" / vehicle."CapacityVolumeM3") ELSE NULL END,
            "PalletOccupancy" = CASE WHEN vehicle."CapacityPallets" > 0 AND route."TotalPallets" IS NOT NULL THEN GREATEST(0, route."TotalPallets"::numeric / vehicle."CapacityPallets") ELSE NULL END,
            "OverallOccupancy" = GREATEST(
                CASE WHEN vehicle."CapacityKg" > 0 THEN GREATEST(0, route."TotalWeightKg" / vehicle."CapacityKg") ELSE NULL END,
                CASE WHEN vehicle."CapacityVolumeM3" > 0 AND route."TotalVolumeM3" IS NOT NULL THEN GREATEST(0, route."TotalVolumeM3" / vehicle."CapacityVolumeM3") ELSE NULL END,
                CASE WHEN vehicle."CapacityPallets" > 0 AND route."TotalPallets" IS NOT NULL THEN GREATEST(0, route."TotalPallets"::numeric / vehicle."CapacityPallets") ELSE NULL END)
        FROM vehicle_types AS vehicle
        WHERE route."VehicleTypeId" = vehicle."Id";
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE routes
        SET "WeightOccupancy" = LEAST("WeightOccupancy", 1),
            "VolumeOccupancy" = LEAST("VolumeOccupancy", 1),
            "PalletOccupancy" = LEAST("PalletOccupancy", 1),
            "OverallOccupancy" = LEAST("OverallOccupancy", 1);
        """);
}
