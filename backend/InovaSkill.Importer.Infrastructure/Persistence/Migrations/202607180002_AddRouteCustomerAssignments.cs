using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607180002_AddRouteCustomerAssignments")]
public sealed class AddRouteCustomerAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE IF NOT EXISTS route_customer_assignments (
            "Id" uuid NOT NULL,
            "RouteId" uuid NOT NULL,
            "CustomerId" uuid NOT NULL,
            "MunicipalityId" uuid NULL,
            "Source" character varying(32) NOT NULL,
            "CreatedAt" timestamptz NOT NULL,
            "UpdatedAt" timestamptz NOT NULL,
            CONSTRAINT "PK_route_customer_assignments" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_route_customer_assignments_routes_RouteId"
                FOREIGN KEY ("RouteId") REFERENCES routes ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_route_customer_assignments_customers_CustomerId"
                FOREIGN KEY ("CustomerId") REFERENCES customers ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_route_customer_assignments_municipalities_MunicipalityId"
                FOREIGN KEY ("MunicipalityId") REFERENCES municipalities ("Id") ON DELETE RESTRICT
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_route_customer_assignments_RouteId_CustomerId"
            ON route_customer_assignments ("RouteId", "CustomerId");
        CREATE INDEX IF NOT EXISTS "IX_route_customer_assignments_RouteId_Source"
            ON route_customer_assignments ("RouteId", "Source");
        CREATE INDEX IF NOT EXISTS "IX_route_customer_assignments_CustomerId_Source"
            ON route_customer_assignments ("CustomerId", "Source");
        CREATE INDEX IF NOT EXISTS "IX_route_customer_assignments_RouteId_MunicipalityId"
            ON route_customer_assignments ("RouteId", "MunicipalityId");

        INSERT INTO route_customer_assignments (
            "Id",
            "RouteId",
            "CustomerId",
            "MunicipalityId",
            "Source",
            "CreatedAt",
            "UpdatedAt")
        SELECT
            (
                substr(md5(route_entries."RouteId"::text || customer_snapshots."CustomerId"::text), 1, 8) || '-' ||
                substr(md5(route_entries."RouteId"::text || customer_snapshots."CustomerId"::text), 9, 4) || '-' ||
                substr(md5(route_entries."RouteId"::text || customer_snapshots."CustomerId"::text), 13, 4) || '-' ||
                substr(md5(route_entries."RouteId"::text || customer_snapshots."CustomerId"::text), 17, 4) || '-' ||
                substr(md5(route_entries."RouteId"::text || customer_snapshots."CustomerId"::text), 21, 12)
            )::uuid,
            route_entries."RouteId",
            customer_snapshots."CustomerId",
            customer_snapshots."MunicipalityId",
            'InferredByMunicipality',
            now(),
            now()
        FROM route_entries
        INNER JOIN routes ON routes."Id" = route_entries."RouteId"
        INNER JOIN data_sources route_source
            ON route_source."Code" = 'ROUTES_BY_CITY'
            AND route_source."CurrentImportId" = routes."ImportId"
        INNER JOIN municipalities
            ON municipalities."Id" = route_entries."MunicipalityId"
            OR (
                route_entries."MunicipalityId" IS NULL
                AND municipalities."NormalizedName" = upper(route_entries."Name")
            )
        INNER JOIN customer_snapshots
            ON customer_snapshots."MunicipalityId" = municipalities."Id"
        INNER JOIN data_sources customer_source
            ON customer_source."Code" = 'CUSTOMERS'
            AND customer_source."CurrentImportId" = customer_snapshots."ImportId"
        INNER JOIN customers ON customers."Id" = customer_snapshots."CustomerId"
        WHERE customers."IsActive" = TRUE
        GROUP BY
            route_entries."RouteId",
            customer_snapshots."CustomerId",
            customer_snapshots."MunicipalityId"
        ON CONFLICT ("RouteId", "CustomerId") DO NOTHING;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS route_customer_assignments;
        """);
}
