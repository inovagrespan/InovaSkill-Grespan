using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609200001_UniqueActiveSimulationCoordinates")]
public class UniqueActiveSimulationCoordinates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE UNIQUE INDEX "IX_customer_coordinate_simulation_audits_active_coordinate"
            ON customer_coordinate_simulation_audits ("SimulatedLatitude", "SimulatedLongitude")
            WHERE "RevertedAt" IS NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX IF EXISTS "IX_customer_coordinate_simulation_audits_active_coordinate";
        """);
}
