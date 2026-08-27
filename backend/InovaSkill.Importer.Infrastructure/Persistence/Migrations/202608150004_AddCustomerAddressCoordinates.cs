using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608150004_AddCustomerAddressCoordinates")]
public sealed class AddCustomerAddressCoordinates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE customer_address_coordinates (
            "Id" uuid NOT NULL,
            "CustomerRegistrationAddressId" uuid NOT NULL,
            "NormalizedAddress" character varying(1024) NOT NULL,
            "Source" character varying(64) NOT NULL,
            "Status" character varying(32) NOT NULL,
            "Latitude" numeric(9,6), "Longitude" numeric(9,6),
            "ProviderPlaceId" character varying(64), "DisplayName" character varying(1024),
            "FailureReason" character varying(1024), "LastAttemptAt" timestamptz,
            "ResolvedAt" timestamptz, "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_customer_address_coordinates" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_customer_address_coordinates_registration_address" FOREIGN KEY ("CustomerRegistrationAddressId")
                REFERENCES customer_registration_addresses ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX "IX_customer_address_coordinates_CustomerRegistrationAddressId"
            ON customer_address_coordinates ("CustomerRegistrationAddressId");
        CREATE INDEX "IX_customer_address_coordinates_NormalizedAddress"
            ON customer_address_coordinates ("NormalizedAddress");
        CREATE INDEX "IX_customer_address_coordinates_Status" ON customer_address_coordinates ("Status");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS customer_address_coordinates;");
}
