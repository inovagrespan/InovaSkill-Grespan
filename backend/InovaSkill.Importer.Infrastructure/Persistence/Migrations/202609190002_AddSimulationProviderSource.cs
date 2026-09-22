using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609190002_AddSimulationProviderSource")]
public class AddSimulationProviderSource : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_coordinate_simulation_audits
            ADD COLUMN "SimulatedSource" character varying(64) NOT NULL
            DEFAULT 'GOOGLE_SIMULATED_NEARBY';
        ALTER TABLE customer_coordinate_simulation_audits
            ALTER COLUMN "SimulatedSource" DROP DEFAULT;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_coordinate_simulation_audits
            DROP COLUMN IF EXISTS "SimulatedSource";
        """);
}
