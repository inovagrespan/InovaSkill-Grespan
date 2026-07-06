using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607050002_PreserveRouteLoadPrecision")]
public sealed class PreserveRouteLoadPrecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE route_entries
            ALTER COLUMN "AveragePerDay" TYPE numeric(18,3);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE route_entries
            ALTER COLUMN "AveragePerDay" TYPE numeric(18,2);
            """);
    }
}
