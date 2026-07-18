using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607080002_AddProductInventoryImports")]
public sealed class AddProductInventoryImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE products ADD COLUMN IF NOT EXISTS "ErpCode" varchar(128) NOT NULL DEFAULT '';
        ALTER TABLE products ADD COLUMN IF NOT EXISTS "OperationalCode" varchar(128) NOT NULL DEFAULT '';
        ALTER TABLE products ADD COLUMN IF NOT EXISTS "Name" varchar(512) NOT NULL DEFAULT '';
        ALTER TABLE products ADD COLUMN IF NOT EXISTS "Type" varchar(64) NOT NULL DEFAULT '';
        ALTER TABLE products ADD COLUMN IF NOT EXISTS "Unit" varchar(32) NOT NULL DEFAULT '';
        ALTER TABLE products ADD COLUMN IF NOT EXISTS "GroupCode" varchar(128) NOT NULL DEFAULT '';
        ALTER TABLE products ADD COLUMN IF NOT EXISTS "NetWeightKg" numeric(18,6) NULL;
        ALTER TABLE products ADD COLUMN IF NOT EXISTS "GrossWeightKg" numeric(18,6) NULL;
        ALTER TABLE products ADD COLUMN IF NOT EXISTS "Gtin" varchar(64) NOT NULL DEFAULT '';

        UPDATE products
        SET "ErpCode" = COALESCE(NULLIF("ErpCode", ''), "ExternalCode"),
            "Name" = COALESCE(NULLIF("Name", ''), "Description");

        ALTER TABLE products ALTER COLUMN "DataSourceId" DROP NOT NULL;
        ALTER TABLE products DROP CONSTRAINT IF EXISTS "UX_products_source_code";
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_products_ErpCode" ON products ("ErpCode");
        CREATE INDEX IF NOT EXISTS "IX_products_OperationalCode" ON products ("OperationalCode");
        CREATE INDEX IF NOT EXISTS "IX_products_GroupCode" ON products ("GroupCode");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_products_DataSourceId_ExternalCode" ON products ("DataSourceId", "ExternalCode");

        CREATE TABLE IF NOT EXISTS inventory_snapshots (
            "Id" uuid PRIMARY KEY,
            "ImportId" uuid NOT NULL REFERENCES imports("Id") ON DELETE CASCADE,
            "ProductId" uuid NOT NULL REFERENCES products("Id") ON DELETE RESTRICT,
            "BranchCode" varchar(64) NOT NULL,
            "WarehouseCode" varchar(64) NOT NULL,
            "OnHandQuantity" numeric(18,6) NOT NULL,
            "CommittedQuantity" numeric(18,6) NOT NULL,
            "AvailableQuantity" numeric(18,6) NOT NULL,
            "StockValue" numeric(18,2) NOT NULL,
            "CommittedValue" numeric(18,2) NOT NULL,
            "SourceRowNumber" integer NOT NULL,
            "CreatedAt" timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_inventory_snapshots_ImportId_ProductId_BranchCode_WarehouseCode"
            ON inventory_snapshots ("ImportId", "ProductId", "BranchCode", "WarehouseCode");
        CREATE INDEX IF NOT EXISTS "IX_inventory_snapshots_ImportId_AvailableQuantity"
            ON inventory_snapshots ("ImportId", "AvailableQuantity");
        CREATE INDEX IF NOT EXISTS "IX_inventory_snapshots_ProductId"
            ON inventory_snapshots ("ProductId");

        CREATE TABLE IF NOT EXISTS daily_inventory_records (
            "Id" uuid PRIMARY KEY,
            "ImportId" uuid NOT NULL REFERENCES imports("Id") ON DELETE CASCADE,
            "ProductId" uuid NOT NULL REFERENCES products("Id") ON DELETE RESTRICT,
            "Date" date NOT NULL,
            "ProductionQuantity" numeric(18,6) NOT NULL,
            "OutboundQuantity" numeric(18,6) NOT NULL,
            "AdjustmentQuantity" numeric(18,6) NOT NULL,
            "ClosingQuantity" numeric(18,6) NOT NULL,
            "FirstShiftProductionQuantity" numeric(18,6) NULL,
            "SecondShiftProductionQuantity" numeric(18,6) NULL,
            "ThirdShiftProductionQuantity" numeric(18,6) NULL,
            "SourceRowNumber" integer NOT NULL,
            "SourceSheetName" varchar(128) NOT NULL,
            "CreatedAt" timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_daily_inventory_records_ImportId_ProductId_Date"
            ON daily_inventory_records ("ImportId", "ProductId", "Date");
        CREATE INDEX IF NOT EXISTS "IX_daily_inventory_records_ProductId_Date"
            ON daily_inventory_records ("ProductId", "Date");
        CREATE INDEX IF NOT EXISTS "IX_daily_inventory_records_Date"
            ON daily_inventory_records ("Date");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS daily_inventory_records;
        DROP TABLE IF EXISTS inventory_snapshots;
        DROP INDEX IF EXISTS "IX_products_GroupCode";
        DROP INDEX IF EXISTS "IX_products_OperationalCode";
        DROP INDEX IF EXISTS "IX_products_ErpCode";
        """);
}
