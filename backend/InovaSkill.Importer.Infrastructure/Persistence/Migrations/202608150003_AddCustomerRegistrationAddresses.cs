using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608150003_AddCustomerRegistrationAddresses")]
public sealed class AddCustomerRegistrationAddresses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE customer_registration_addresses (
            "Id" uuid NOT NULL,
            "CustomerId" uuid NOT NULL,
            "DocumentNumber" character varying(32) NOT NULL,
            "Source" character varying(64) NOT NULL,
            "Status" character varying(32) NOT NULL,
            "PostalCode" character varying(16),
            "StateCode" character varying(2),
            "City" character varying(256),
            "Street" character varying(512),
            "Number" character varying(64),
            "Complement" character varying(256),
            "Neighborhood" character varying(256),
            "FailureReason" character varying(1024),
            "LastAttemptAt" timestamptz,
            "ResolvedAt" timestamptz,
            "CreatedAt" timestamptz NOT NULL,
            "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_customer_registration_addresses" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_customer_registration_addresses_customers_CustomerId"
                FOREIGN KEY ("CustomerId") REFERENCES customers ("Id") ON DELETE CASCADE,
            CONSTRAINT "UX_customer_registration_addresses_CustomerId" UNIQUE ("CustomerId")
        );
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS customer_registration_addresses;
        """);
}
