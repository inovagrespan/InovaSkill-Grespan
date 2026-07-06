using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607060001_AddCustomersAndMunicipalities")]
public sealed class AddCustomersAndMunicipalities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE municipalities (
                "Id" uuid PRIMARY KEY,
                "StateCode" varchar(2) NOT NULL,
                "Name" varchar(256) NOT NULL,
                "NormalizedName" varchar(256) NOT NULL,
                "IbgeCode" varchar(7) NULL,
                "CreatedAt" timestamptz NOT NULL,
                CONSTRAINT "UX_municipalities_state_name" UNIQUE ("StateCode", "NormalizedName")
            );
            CREATE TABLE customers (
                "Id" uuid PRIMARY KEY,
                "DataSourceId" uuid NOT NULL REFERENCES data_sources("Id") ON DELETE RESTRICT,
                "BranchCode" varchar(64) NOT NULL,
                "ExternalCode" varchar(128) NOT NULL,
                "CreatedAt" timestamptz NOT NULL,
                CONSTRAINT "UX_customers_identity" UNIQUE ("DataSourceId", "BranchCode", "ExternalCode")
            );
            CREATE TABLE customer_snapshots (
                "Id" uuid PRIMARY KEY,
                "ImportId" uuid NOT NULL REFERENCES imports("Id") ON DELETE CASCADE,
                "CustomerId" uuid NOT NULL REFERENCES customers("Id") ON DELETE RESTRICT,
                "DocumentNumber" varchar(32) NOT NULL,
                "DocumentType" varchar(16) NOT NULL,
                "LegalName" varchar(512) NOT NULL,
                "TradeName" varchar(512) NOT NULL,
                "CustomerType" varchar(128) NOT NULL,
                "MunicipalityId" uuid NOT NULL REFERENCES municipalities("Id") ON DELETE RESTRICT,
                "SourceRowNumber" integer NOT NULL,
                "CreatedAt" timestamptz NOT NULL,
                CONSTRAINT "UX_customer_snapshots_import_customer" UNIQUE ("ImportId", "CustomerId")
            );
            ALTER TABLE route_entries ADD COLUMN "MunicipalityId" uuid NULL;
            ALTER TABLE route_entries ADD CONSTRAINT "FK_route_entries_municipalities_MunicipalityId"
                FOREIGN KEY ("MunicipalityId") REFERENCES municipalities("Id") ON DELETE RESTRICT;
            CREATE INDEX "IX_route_entries_MunicipalityId" ON route_entries ("MunicipalityId");
            CREATE INDEX "IX_customer_snapshots_MunicipalityId" ON customer_snapshots ("MunicipalityId");
            CREATE INDEX "IX_customer_snapshots_ImportId" ON customer_snapshots ("ImportId");
            """);
        // Route entries do not contain UF. Existing rows therefore remain unlinked rather
        // than receiving a potentially incorrect municipality.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE route_entries DROP CONSTRAINT IF EXISTS "FK_route_entries_municipalities_MunicipalityId";
            ALTER TABLE route_entries DROP COLUMN IF EXISTS "MunicipalityId";
            DROP TABLE IF EXISTS customer_snapshots;
            DROP TABLE IF EXISTS customers;
            DROP TABLE IF EXISTS municipalities;
            """);
    }
}
