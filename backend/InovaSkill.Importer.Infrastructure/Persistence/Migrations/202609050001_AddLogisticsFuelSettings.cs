using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202609050001_AddLogisticsFuelSettings")]
public sealed class AddLogisticsFuelSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE logistics_fuel_settings (
            "Id" uuid PRIMARY KEY,
            "DieselPricePerLiter" numeric(10,3) NOT NULL,
            "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "CK_logistics_fuel_settings_DieselPricePerLiter"
                CHECK ("DieselPricePerLiter" > 0)
        );
        INSERT INTO logistics_fuel_settings ("Id", "DieselPricePerLiter", "UpdatedAt")
        VALUES ('8f5e6ee8-3865-4be7-8ec7-b891f3656686', 6.900, NOW());
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS logistics_fuel_settings;");
}
