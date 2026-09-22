using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609200002_PreserveHistoricalRouteCosts")]
public class PreserveHistoricalRouteCosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "route_cost_items_OptimizationResultId_fkey",
            table: "route_cost_items");

        migrationBuilder.AddForeignKey(
            name: "route_cost_items_OptimizationResultId_fkey",
            table: "route_cost_items",
            column: "OptimizationResultId",
            principalTable: "daily_route_optimization_results",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "route_cost_items_OptimizationResultId_fkey",
            table: "route_cost_items");

        migrationBuilder.AddForeignKey(
            name: "route_cost_items_OptimizationResultId_fkey",
            table: "route_cost_items",
            column: "OptimizationResultId",
            principalTable: "daily_route_optimization_results",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
