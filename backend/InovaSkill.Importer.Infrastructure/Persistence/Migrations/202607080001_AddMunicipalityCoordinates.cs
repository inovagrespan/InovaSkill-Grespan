using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607080001_AddMunicipalityCoordinates")]
public sealed class AddMunicipalityCoordinates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE municipality_coordinates (
                "Id" uuid PRIMARY KEY,
                "MunicipalityId" uuid NOT NULL REFERENCES municipalities("Id") ON DELETE CASCADE,
                "Latitude" numeric(9,6) NULL,
                "Longitude" numeric(9,6) NULL,
                "Source" varchar(160) NOT NULL,
                "Status" varchar(32) NOT NULL,
                "FailureReason" varchar(1024) NULL,
                "LastAttemptAt" timestamptz NULL,
                "ResolvedAt" timestamptz NULL,
                "CreatedAt" timestamptz NOT NULL,
                "UpdatedAt" timestamptz NOT NULL,
                CONSTRAINT "UX_municipality_coordinates_municipality" UNIQUE ("MunicipalityId")
            );
            CREATE INDEX "IX_municipality_coordinates_Status" ON municipality_coordinates ("Status");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS municipality_coordinates;
            """);
    }
}
