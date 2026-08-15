using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202608150002_AddCustomerRouteMappings")]
public sealed class AddCustomerRouteMappings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE customer_route_mappings (
            "Id" uuid PRIMARY KEY,
            "ImportId" uuid NOT NULL REFERENCES imports("Id") ON DELETE CASCADE,
            "SheetName" varchar(128) NOT NULL,
            "SourceRowNumber" integer NOT NULL,
            "CustomerId" uuid NOT NULL REFERENCES customers("Id") ON DELETE RESTRICT,
            "Weekday" varchar(16) NOT NULL,
            "RouteName" varchar(256) NOT NULL,
            "NormalizedRouteName" varchar(256) NOT NULL,
            "MarketName" varchar(512) NOT NULL,
            "MunicipalityName" varchar(256) NOT NULL,
            "CreatedAt" timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX "IX_customer_route_mappings_ImportId_SourceRowNumber_SheetName"
            ON customer_route_mappings ("ImportId", "SourceRowNumber", "SheetName");
        CREATE INDEX "IX_customer_route_mappings_ImportId_Weekday_NormalizedRouteName"
            ON customer_route_mappings ("ImportId", "Weekday", "NormalizedRouteName");
        CREATE INDEX "IX_customer_route_mappings_ImportId_CustomerId"
            ON customer_route_mappings ("ImportId", "CustomerId");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "customer_route_mappings");
}
