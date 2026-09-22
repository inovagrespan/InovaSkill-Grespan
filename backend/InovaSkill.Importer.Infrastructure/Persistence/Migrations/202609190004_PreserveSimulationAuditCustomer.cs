using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609190004_PreserveSimulationAuditCustomer")]
public class PreserveSimulationAuditCustomer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_coordinate_simulation_audits ADD COLUMN "CustomerId" uuid;
        UPDATE customer_coordinate_simulation_audits audit
        SET "CustomerId" = address."CustomerId"
        FROM customer_registration_addresses address
        WHERE address."Id" = audit."CustomerRegistrationAddressId";
        ALTER TABLE customer_coordinate_simulation_audits ALTER COLUMN "CustomerId" SET NOT NULL;
        ALTER TABLE customer_coordinate_simulation_audits
            ADD CONSTRAINT "FK_customer_coordinate_simulation_audits_customer"
            FOREIGN KEY ("CustomerId") REFERENCES customers ("Id") ON DELETE RESTRICT;
        ALTER TABLE customer_coordinate_simulation_audits
            DROP CONSTRAINT "FK_customer_coordinate_simulation_audits_address";
        ALTER TABLE customer_coordinate_simulation_audits
            ALTER COLUMN "CustomerRegistrationAddressId" DROP NOT NULL;
        ALTER TABLE customer_coordinate_simulation_audits
            ADD CONSTRAINT "FK_customer_coordinate_simulation_audits_address"
            FOREIGN KEY ("CustomerRegistrationAddressId")
            REFERENCES customer_registration_addresses ("Id") ON DELETE SET NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_coordinate_simulation_audits
            DROP CONSTRAINT "FK_customer_coordinate_simulation_audits_address";
        ALTER TABLE customer_coordinate_simulation_audits
            ALTER COLUMN "CustomerRegistrationAddressId" SET NOT NULL;
        ALTER TABLE customer_coordinate_simulation_audits
            ADD CONSTRAINT "FK_customer_coordinate_simulation_audits_address"
            FOREIGN KEY ("CustomerRegistrationAddressId")
            REFERENCES customer_registration_addresses ("Id") ON DELETE RESTRICT;
        ALTER TABLE customer_coordinate_simulation_audits
            DROP CONSTRAINT "FK_customer_coordinate_simulation_audits_customer";
        ALTER TABLE customer_coordinate_simulation_audits DROP COLUMN "CustomerId";
        """);
}
