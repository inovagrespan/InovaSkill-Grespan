using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609200001_AddOptimizationStopCustomer")]
public class AddOptimizationStopCustomer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE daily_route_optimization_stops ADD COLUMN "CustomerId" uuid;
        ALTER TABLE daily_route_optimization_stops
            ADD CONSTRAINT "FK_daily_route_optimization_stops_customer"
            FOREIGN KEY ("CustomerId") REFERENCES customers ("Id") ON DELETE RESTRICT;
        CREATE INDEX "IX_daily_route_optimization_stops_CustomerId"
            ON daily_route_optimization_stops ("CustomerId");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX IF EXISTS "IX_daily_route_optimization_stops_CustomerId";
        ALTER TABLE daily_route_optimization_stops
            DROP CONSTRAINT IF EXISTS "FK_daily_route_optimization_stops_customer";
        ALTER TABLE daily_route_optimization_stops DROP COLUMN IF EXISTS "CustomerId";
        """);
}
