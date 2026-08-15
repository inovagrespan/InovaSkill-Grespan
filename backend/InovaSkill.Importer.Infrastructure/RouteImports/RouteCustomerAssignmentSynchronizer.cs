using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public interface IRouteCustomerAssignmentSynchronizer
{
    Task SyncInferredAssignmentsAsync(CancellationToken cancellationToken);
}

public sealed class RouteCustomerAssignmentSynchronizer(ImportDbContext dbContext)
    : IRouteCustomerAssignmentSynchronizer
{
    public async Task SyncInferredAssignmentsAsync(CancellationToken cancellationToken)
    {
        var routeImportId = await CurrentImportIdAsync(RouteImportCodes.DataSource, cancellationToken);
        var customerImportId = await CurrentImportIdAsync(CustomerImportCodes.DataSource, cancellationToken);
        if (!routeImportId.HasValue || !customerImportId.HasValue)
        {
            return;
        }

        var mappingImportId = await CurrentImportIdAsync(
            CustomerRouteAssignmentImportCodes.DataSource, cancellationToken);
        if (mappingImportId.HasValue)
        {
            await SyncImportedAssignmentsAsync(routeImportId.Value, customerImportId.Value,
                mappingImportId.Value, cancellationToken);
            return;
        }

        if (dbContext.Database.IsNpgsql())
        {
            await SyncInferredAssignmentsInPostgresAsync(
                routeImportId.Value,
                customerImportId.Value,
                cancellationToken);
            return;
        }

        await SyncInferredAssignmentsInTrackedContextAsync(
            routeImportId.Value,
            customerImportId.Value,
            cancellationToken);
    }

    private async Task SyncImportedAssignmentsAsync(
        Guid routeImportId, Guid customerImportId, Guid mappingImportId,
        CancellationToken cancellationToken)
    {
        await dbContext.RouteCustomerAssignments.ExecuteDeleteAsync(cancellationToken);
        var currentCustomerIds = (await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(x => x.ImportId == customerImportId).Select(x => x.CustomerId).ToListAsync(cancellationToken))
            .ToHashSet();
        var mappings = await dbContext.CustomerRouteMappings.AsNoTracking()
            .Where(x => x.ImportId == mappingImportId && currentCustomerIds.Contains(x.CustomerId))
            .ToListAsync(cancellationToken);
        var routes = await dbContext.Routes.AsNoTracking().Where(x => x.ImportId == routeImportId)
            .Select(x => new { x.Id, x.Name, x.Weekday }).ToListAsync(cancellationToken);
        var municipalities = await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(x => x.ImportId == customerImportId)
            .ToDictionaryAsync(x => x.CustomerId, x => x.MunicipalityId, cancellationToken);
        var now = DateTime.UtcNow;
        var assignments = mappings.Select(mapping => new
            {
                Mapping = mapping,
                Route = routes.SingleOrDefault(route => route.Weekday == mapping.Weekday &&
                    CustomerRouteAssignmentsSpreadsheetParser.Normalize(route.Name) == mapping.NormalizedRouteName)
            })
            .Where(x => x.Route is not null)
            .DistinctBy(x => new { RouteId = x.Route!.Id, x.Mapping.CustomerId })
            .Select(x => new RouteCustomerAssignment
            {
                Id = Guid.NewGuid(), RouteId = x.Route!.Id, CustomerId = x.Mapping.CustomerId,
                MunicipalityId = municipalities.GetValueOrDefault(x.Mapping.CustomerId),
                Source = RouteCustomerAssignmentSource.Imported, CreatedAt = now, UpdatedAt = now
            });
        dbContext.RouteCustomerAssignments.AddRange(assignments);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncInferredAssignmentsInPostgresAsync(
        Guid routeImportId,
        Guid customerImportId,
        CancellationToken cancellationToken)
    {
        await dbContext.RouteCustomerAssignments
            .Where(assignment => assignment.Source == RouteCustomerAssignmentSource.InferredByMunicipality)
            .ExecuteDeleteAsync(cancellationToken);

        var now = DateTime.UtcNow;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
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
                {now},
                {now}
            FROM route_entries
            INNER JOIN routes ON routes."Id" = route_entries."RouteId"
            INNER JOIN municipalities
                ON municipalities."Id" = route_entries."MunicipalityId"
                OR (
                    route_entries."MunicipalityId" IS NULL
                    AND municipalities."NormalizedName" = upper(route_entries."Name")
                )
            INNER JOIN customer_snapshots
                ON customer_snapshots."MunicipalityId" = municipalities."Id"
            INNER JOIN customers ON customers."Id" = customer_snapshots."CustomerId"
            WHERE routes."ImportId" = {routeImportId}
                AND customer_snapshots."ImportId" = {customerImportId}
                AND customers."IsActive" = TRUE
            GROUP BY
                route_entries."RouteId",
                customer_snapshots."CustomerId",
                customer_snapshots."MunicipalityId"
            ON CONFLICT ("RouteId", "CustomerId") DO NOTHING
            """, cancellationToken);
    }

    private async Task SyncInferredAssignmentsInTrackedContextAsync(
        Guid routeImportId,
        Guid customerImportId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.RouteCustomerAssignments
            .Where(assignment => assignment.Source == RouteCustomerAssignmentSource.InferredByMunicipality)
            .ToListAsync(cancellationToken);
        dbContext.RouteCustomerAssignments.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var routeEntries = await dbContext.RouteEntries.AsNoTracking()
            .Where(entry => entry.Route!.ImportId == routeImportId)
            .Select(entry => new
            {
                entry.RouteId,
                entry.MunicipalityId,
                entry.Name
            })
            .ToListAsync(cancellationToken);
        var routeMunicipalityNames = routeEntries
            .Where(entry => entry.MunicipalityId is null)
            .Select(entry => MunicipalityNameNormalizer.Normalize(entry.Name))
            .Distinct()
            .ToArray();
        var inferredMunicipalities = await dbContext.Municipalities.AsNoTracking()
            .Where(municipality => routeMunicipalityNames.Contains(municipality.NormalizedName))
            .Select(municipality => new { municipality.Id, municipality.NormalizedName })
            .ToListAsync(cancellationToken);
        var inferredMunicipalityIdsByName = inferredMunicipalities
            .GroupBy(municipality => municipality.NormalizedName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(municipality => municipality.Id).ToArray());
        var routeMunicipalityPairs = routeEntries
            .SelectMany(entry =>
            {
                if (entry.MunicipalityId.HasValue)
                {
                    return [new { entry.RouteId, MunicipalityId = entry.MunicipalityId.Value }];
                }

                var normalizedName = MunicipalityNameNormalizer.Normalize(entry.Name);
                return inferredMunicipalityIdsByName.TryGetValue(normalizedName, out var municipalityIds)
                    ? municipalityIds.Select(municipalityId => new { entry.RouteId, MunicipalityId = municipalityId })
                    : [];
            })
            .Distinct()
            .ToArray();
        var municipalityIds = routeMunicipalityPairs
            .Select(pair => pair.MunicipalityId)
            .Distinct()
            .ToArray();
        var customerSnapshots = await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(snapshot =>
                snapshot.ImportId == customerImportId &&
                municipalityIds.Contains(snapshot.MunicipalityId) &&
                snapshot.Customer!.IsActive)
            .Select(snapshot => new
            {
                snapshot.CustomerId,
                snapshot.MunicipalityId
            })
            .ToListAsync(cancellationToken);
        var pairs = routeMunicipalityPairs
            .Join(
                customerSnapshots,
                routeMunicipality => routeMunicipality.MunicipalityId,
                customer => customer.MunicipalityId,
                (routeMunicipality, customer) => new
                {
                    routeMunicipality.RouteId,
                    customer.CustomerId,
                    customer.MunicipalityId
                })
            .Distinct()
            .ToArray();

        dbContext.RouteCustomerAssignments.AddRange(pairs.Select(pair => new RouteCustomerAssignment
        {
            Id = Guid.NewGuid(),
            RouteId = pair.RouteId,
            CustomerId = pair.CustomerId,
            MunicipalityId = pair.MunicipalityId,
            Source = RouteCustomerAssignmentSource.InferredByMunicipality,
            CreatedAt = now,
            UpdatedAt = now
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid?> CurrentImportIdAsync(string sourceCode, CancellationToken cancellationToken) =>
        await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == sourceCode)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
}
