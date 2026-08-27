using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608150006_AddLogisticsDepot")]
public sealed class AddLogisticsDepot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE logistics_depots (
                "Id" uuid PRIMARY KEY,
                "SingletonKey" smallint NOT NULL DEFAULT 1,
                "Name" varchar(160) NOT NULL,
                "Address" varchar(500) NOT NULL,
                "Latitude" numeric(9,6) NOT NULL,
                "Longitude" numeric(9,6) NOT NULL,
                "CreatedAt" timestamptz NOT NULL,
                "UpdatedAt" timestamptz NOT NULL,
                CONSTRAINT "CK_logistics_depots_singleton" CHECK ("SingletonKey" = 1),
                CONSTRAINT "CK_logistics_depots_latitude" CHECK ("Latitude" BETWEEN -90 AND 90),
                CONSTRAINT "CK_logistics_depots_longitude" CHECK ("Longitude" BETWEEN -180 AND 180)
            );
            CREATE UNIQUE INDEX "UX_logistics_depots_singleton" ON logistics_depots ("SingletonKey");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS logistics_depots;");
}
