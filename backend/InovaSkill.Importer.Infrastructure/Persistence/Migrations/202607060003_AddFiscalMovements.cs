using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607060003_AddFiscalMovements")]
public sealed class AddFiscalMovements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE products (
            "Id" uuid PRIMARY KEY, "DataSourceId" uuid NOT NULL REFERENCES data_sources("Id") ON DELETE RESTRICT,
            "ExternalCode" varchar(128) NOT NULL, "Description" varchar(512) NOT NULL,
            "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "UX_products_source_code" UNIQUE ("DataSourceId", "ExternalCode"));
        CREATE TABLE fiscal_documents (
            "Id" uuid PRIMARY KEY, "DataSourceId" uuid NOT NULL REFERENCES data_sources("Id") ON DELETE RESTRICT,
            "DocumentNumber" varchar(128) NOT NULL, "Series" varchar(64) NOT NULL,
            "DocumentType" varchar(64) NOT NULL, "MovementType" varchar(64) NOT NULL, "IssueDate" date NOT NULL,
            "CustomerId" uuid NULL REFERENCES customers("Id") ON DELETE SET NULL,
            "MunicipalityId" uuid NULL REFERENCES municipalities("Id") ON DELETE SET NULL,
            "CustomerCodeAtIssue" varchar(128) NOT NULL, "BranchCodeAtIssue" varchar(64) NOT NULL,
            "CustomerNameAtIssue" varchar(512) NOT NULL, "CityNameAtIssue" varchar(256) NOT NULL,
            "StateCodeAtIssue" varchar(2) NOT NULL, "OperationCode" varchar(128) NOT NULL,
            "OperationDescription" varchar(256) NOT NULL, "MovementCategory" varchar(16) NOT NULL,
            "OriginalDocumentNumber" varchar(128) NULL,
            "FirstSeenImportId" uuid NOT NULL REFERENCES imports("Id") ON DELETE RESTRICT,
            "LastSeenImportId" uuid NOT NULL REFERENCES imports("Id") ON DELETE RESTRICT,
            "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "UX_fiscal_documents_business_key" UNIQUE
                ("DataSourceId","DocumentType","DocumentNumber","Series","IssueDate","CustomerCodeAtIssue","BranchCodeAtIssue"));
        CREATE TABLE fiscal_document_items (
            "Id" uuid PRIMARY KEY, "FiscalDocumentId" uuid NOT NULL REFERENCES fiscal_documents("Id") ON DELETE CASCADE,
            "ItemNumber" varchar(64) NOT NULL, "ProductId" uuid NULL REFERENCES products("Id") ON DELETE SET NULL,
            "ProductCode" varchar(128) NOT NULL, "ProductDescription" varchar(512) NOT NULL,
            "ProductGroupCode" varchar(128) NOT NULL, "ProductGroupDescription" varchar(256) NOT NULL,
            "Quantity" numeric(18,6) NOT NULL, "GrossWeightKg" numeric(18,3) NOT NULL,
            "UnitValue" numeric(18,6) NULL, "SourceTotalValue" numeric(18,2) NULL,
            "Expenses" numeric(18,2) NULL, "Ipi" numeric(18,2) NULL, "Icms" numeric(18,2) NULL, "Iss" numeric(18,2) NULL,
            "CfopCode" text NULL, "CfopDescription" text NULL, "TesCode" text NULL, "TesDescription" text NULL,
            "OrderNumber" text NULL, "WarehouseCode" text NULL, "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "UX_fiscal_items_document_item" UNIQUE ("FiscalDocumentId","ItemNumber"));
        CREATE INDEX "IX_fiscal_documents_customer_date_category" ON fiscal_documents ("CustomerId","IssueDate","MovementCategory");
        CREATE INDEX "IX_fiscal_documents_date_category" ON fiscal_documents ("IssueDate","MovementCategory");
        CREATE INDEX "IX_fiscal_documents_number" ON fiscal_documents ("DocumentNumber");
        CREATE INDEX "IX_fiscal_items_product" ON fiscal_document_items ("ProductId");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS fiscal_document_items;
        DROP TABLE IF EXISTS fiscal_documents;
        DROP TABLE IF EXISTS products;
        """);
}
