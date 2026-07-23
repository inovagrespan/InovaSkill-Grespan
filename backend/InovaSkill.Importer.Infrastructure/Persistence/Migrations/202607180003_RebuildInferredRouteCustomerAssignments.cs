using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InovaSkill.Importer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImportDbContext))]
[Migration("202607180003_RebuildInferredRouteCustomerAssignments")]
public sealed class RebuildInferredRouteCustomerAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM route_customer_assignments
        WHERE "Source" = 'InferredByMunicipality';

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
        DELETE FROM route_customer_assignments
        WHERE "Source" = 'InferredByMunicipality';
        """);
}
