using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608180002_SeedDefaultLogisticsDepot")]
public sealed class SeedDefaultLogisticsDepot : Migration
{
    private const string DefaultDepotId = "4d5fc8aa-0aae-4fd8-b5fc-8d1572e63df0";

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql($$"""
        INSERT INTO logistics_depots
            ("Id", "SingletonKey", "Name", "Address", "Latitude", "Longitude", "CreatedAt", "UpdatedAt")
        SELECT
            '{{DefaultDepotId}}', 1, 'Grespan Matriz',
            'Avenida República, 7000 - Distrito Industrial Santo Barion - Marília/SP - CEP 17512-035',
            -22.213890, -49.945830, NOW(), NOW()
        WHERE NOT EXISTS (SELECT 1 FROM logistics_depots);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql($$"""
        DELETE FROM logistics_depots WHERE "Id" = '{{DefaultDepotId}}';
        """);
}
