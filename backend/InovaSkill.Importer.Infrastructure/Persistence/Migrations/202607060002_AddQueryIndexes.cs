using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607060002_AddQueryIndexes")]
public sealed class AddQueryIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS pg_trgm;

            CREATE INDEX "IX_routes_ImportId_OverallOccupancy"
                ON routes ("ImportId", "OverallOccupancy");
            CREATE INDEX "IX_routes_Name_trgm"
                ON routes USING gin (upper("Name") gin_trgm_ops);
            CREATE INDEX "IX_route_entries_Name_trgm"
                ON route_entries USING gin (upper("Name") gin_trgm_ops);

            CREATE INDEX "IX_customers_DataSourceId_ExternalCode_BranchCode"
                ON customers ("DataSourceId", "ExternalCode", "BranchCode");
            CREATE INDEX "IX_customer_snapshots_ImportId_MunicipalityId"
                ON customer_snapshots ("ImportId", "MunicipalityId");
            CREATE INDEX "IX_customer_snapshots_ImportId_CustomerType"
                ON customer_snapshots ("ImportId", "CustomerType");
            CREATE INDEX "IX_customer_snapshots_LegalName_trgm"
                ON customer_snapshots USING gin (upper("LegalName") gin_trgm_ops);
            CREATE INDEX "IX_customer_snapshots_TradeName_trgm"
                ON customer_snapshots USING gin (upper("TradeName") gin_trgm_ops);
            CREATE INDEX "IX_customer_snapshots_DocumentNumber_trgm"
                ON customer_snapshots USING gin (upper("DocumentNumber") gin_trgm_ops);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_customer_snapshots_DocumentNumber_trgm";
            DROP INDEX IF EXISTS "IX_customer_snapshots_TradeName_trgm";
            DROP INDEX IF EXISTS "IX_customer_snapshots_LegalName_trgm";
            DROP INDEX IF EXISTS "IX_customer_snapshots_ImportId_CustomerType";
            DROP INDEX IF EXISTS "IX_customer_snapshots_ImportId_MunicipalityId";
            DROP INDEX IF EXISTS "IX_customers_DataSourceId_ExternalCode_BranchCode";
            DROP INDEX IF EXISTS "IX_route_entries_Name_trgm";
            DROP INDEX IF EXISTS "IX_routes_Name_trgm";
            DROP INDEX IF EXISTS "IX_routes_ImportId_OverallOccupancy";
            """);
    }
}
