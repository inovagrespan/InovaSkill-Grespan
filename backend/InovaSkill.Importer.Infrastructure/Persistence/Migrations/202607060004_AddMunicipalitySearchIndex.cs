using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607060004_AddMunicipalitySearchIndex")]
public sealed class AddMunicipalitySearchIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS pg_trgm;
            CREATE INDEX "IX_municipalities_NormalizedName_trgm"
                ON municipalities USING gin ("NormalizedName" gin_trgm_ops);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_municipalities_NormalizedName_trgm";
            """);
    }
}
