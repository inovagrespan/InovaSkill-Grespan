using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609140001_AddRouteDepartureTime")]
public sealed class AddRouteDepartureTime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<TimeOnly>(
        name: "DepartureTime",
        table: "routes",
        type: "time without time zone",
        nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "DepartureTime",
        table: "routes");
}
