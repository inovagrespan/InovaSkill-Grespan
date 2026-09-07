using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608180003_AddCustomerDeliveryProfiles")]
public sealed class AddCustomerDeliveryProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE customer_delivery_profiles (
            "Id" uuid PRIMARY KEY,
            "ImportId" uuid NOT NULL REFERENCES imports("Id") ON DELETE CASCADE,
            "CustomerId" uuid NOT NULL REFERENCES customers("Id") ON DELETE RESTRICT,
            "TradeName" varchar(512) NOT NULL,
            "MunicipalityName" varchar(256) NOT NULL,
            "DeliveryAddress" varchar(512) NOT NULL,
            "Classification" varchar(1) NOT NULL,
            "PostalCode" varchar(9) NOT NULL,
            "SourceRowNumber" integer NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            CONSTRAINT "CK_customer_delivery_profiles_classification"
                CHECK ("Classification" IN ('A', 'B', 'C', 'D', 'E', 'F', 'G')),
            CONSTRAINT "CK_customer_delivery_profiles_postal_code"
                CHECK ("PostalCode" ~ '^[0-9]{5}-[0-9]{3}$')
        );
        CREATE UNIQUE INDEX "IX_customer_delivery_profiles_ImportId_SourceRowNumber"
            ON customer_delivery_profiles ("ImportId", "SourceRowNumber");
        CREATE UNIQUE INDEX "IX_customer_delivery_profiles_ImportId_CustomerId"
            ON customer_delivery_profiles ("ImportId", "CustomerId");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "customer_delivery_profiles");
}
