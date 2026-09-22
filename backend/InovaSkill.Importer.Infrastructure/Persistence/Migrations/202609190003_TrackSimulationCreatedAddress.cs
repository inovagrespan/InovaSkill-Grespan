using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609190003_TrackSimulationCreatedAddress")]
public class TrackSimulationCreatedAddress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_coordinate_simulation_audits
            ADD COLUMN "RegistrationAddressCreatedBySimulation" boolean NOT NULL DEFAULT false;
        ALTER TABLE customer_coordinate_simulation_audits
            ALTER COLUMN "RegistrationAddressCreatedBySimulation" DROP DEFAULT;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_coordinate_simulation_audits
            DROP COLUMN IF EXISTS "RegistrationAddressCreatedBySimulation";
        """);
}
