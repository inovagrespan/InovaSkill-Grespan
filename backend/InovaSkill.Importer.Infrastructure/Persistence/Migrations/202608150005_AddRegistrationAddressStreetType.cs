using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608150005_AddRegistrationAddressStreetType")]
public sealed class AddRegistrationAddressStreetType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_registration_addresses
            ADD COLUMN "StreetType" character varying(64);

        -- As coordenadas anteriores não confirmavam o número do imóvel e são
        -- dados derivados recuperáveis. Elas devem ser recalculadas com o endereço formatado.
        DELETE FROM customer_address_coordinates;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_registration_addresses DROP COLUMN IF EXISTS "StreetType";
        """);
}
