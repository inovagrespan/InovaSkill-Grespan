using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608010004_AddFiscalImportStaging")]
public sealed class AddFiscalImportStaging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE fiscal_import_staging (
            "ImportId" uuid NOT NULL REFERENCES imports("Id") ON DELETE CASCADE,
            "RowNumber" integer NOT NULL,
            "CustomerId" uuid NULL,
            "MunicipalityId" uuid NULL,
            "MovementCategory" varchar(16) NOT NULL,
            "Payload" jsonb NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_fiscal_import_staging" PRIMARY KEY ("ImportId", "RowNumber")
        );
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS fiscal_import_staging;");
}
