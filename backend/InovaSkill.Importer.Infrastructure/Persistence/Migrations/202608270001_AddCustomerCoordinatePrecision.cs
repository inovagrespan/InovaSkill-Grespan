using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608270001_AddCustomerCoordinatePrecision")]
public sealed class AddCustomerCoordinatePrecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_address_coordinates
        ADD COLUMN IF NOT EXISTS "Precision" character varying(32) NOT NULL DEFAULT 'EXACT';
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_address_coordinates DROP COLUMN IF EXISTS "Precision";
        """);
}
