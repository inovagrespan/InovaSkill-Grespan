using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608250001_ExpandCustomerCoordinateProviderPlaceId")]
public sealed class ExpandCustomerCoordinateProviderPlaceId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_address_coordinates
            ALTER COLUMN "ProviderPlaceId" TYPE character varying(512);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE customer_address_coordinates
        SET "ProviderPlaceId" = left("ProviderPlaceId", 64)
        WHERE length("ProviderPlaceId") > 64;

        ALTER TABLE customer_address_coordinates
            ALTER COLUMN "ProviderPlaceId" TYPE character varying(64);
        """);
}
