using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609200003_PreserveHistoricalRouteCostVehicle")]
public class PreserveHistoricalRouteCostVehicle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "route_cost_items_OptimizationVehicleId_fkey",
            table: "route_cost_items");

        migrationBuilder.AddForeignKey(
            name: "route_cost_items_OptimizationVehicleId_fkey",
            table: "route_cost_items",
            column: "OptimizationVehicleId",
            principalTable: "daily_route_optimization_vehicles",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "route_cost_items_OptimizationVehicleId_fkey",
            table: "route_cost_items");

        migrationBuilder.AddForeignKey(
            name: "route_cost_items_OptimizationVehicleId_fkey",
            table: "route_cost_items",
            column: "OptimizationVehicleId",
            principalTable: "daily_route_optimization_vehicles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
