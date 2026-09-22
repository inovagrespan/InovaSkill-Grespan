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

        await SyncInferredAssignmentsAsync(
            routeImportId.Value,
            customerImportId.Value,
            cancellationToken);
    }

    private async Task SyncImportedAssignmentsAsync(
        Guid routeImportId, Guid customerImportId, Guid mappingImportId,
        CancellationToken cancellationToken)
    {
        await dbContext.RouteCustomerAssignments
            .Where(assignment => assignment.Source != RouteCustomerAssignmentSource.Manual)
            .ExecuteDeleteAsync(cancellationToken);
        var routes = await dbContext.Routes.AsNoTracking().Where(x => x.ImportId == routeImportId)
            .Select(x => new { x.Id, x.Name, x.Weekday }).ToListAsync(cancellationToken);
        var routeById = routes.ToDictionary(route => route.Id);
        var routeEntries = await dbContext.RouteEntries.AsNoTracking()
            .Where(entry => entry.Route!.ImportId == routeImportId && entry.MunicipalityId.HasValue)
            .Select(entry => new
            {
                entry.RouteId,
                MunicipalityId = entry.MunicipalityId!.Value,
                entry.Deliveries
            })
            .ToListAsync(cancellationToken);
        var customerSnapshots = await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == customerImportId)
            .Select(snapshot => new
            {
                snapshot.CustomerId,
                snapshot.MunicipalityId,
                ExternalCode = snapshot.Customer!.ExternalCode
            })
            .ToListAsync(cancellationToken);
        var customerById = customerSnapshots.ToDictionary(customer => customer.CustomerId);
        var manualAssignments = await dbContext.RouteCustomerAssignments.AsNoTracking()
            .Where(assignment => assignment.Source == RouteCustomerAssignmentSource.Manual)
            .Select(assignment => new
            {
                assignment.RouteId,
                assignment.CustomerId,
                assignment.MunicipalityId
            })
            .ToListAsync(cancellationToken);
        manualAssignments = manualAssignments.Where(assignment => routeById.ContainsKey(assignment.RouteId)).ToList();
        var manualPairs = manualAssignments
            .Select(pair => (pair.RouteId, pair.CustomerId))
            .ToHashSet();
        var usedByWeekday = manualAssignments
            .Where(assignment => customerById.ContainsKey(assignment.CustomerId))
            .GroupBy(assignment => routeById[assignment.RouteId].Weekday, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(assignment => assignment.CustomerId).ToHashSet(),
                StringComparer.Ordinal);
        var manualCountByRouteMunicipality = manualAssignments
            .Select(assignment => new
            {
                assignment.RouteId,
                MunicipalityId = assignment.MunicipalityId ??
                    customerById.GetValueOrDefault(assignment.CustomerId)?.MunicipalityId
            })
            .Where(assignment => assignment.MunicipalityId.HasValue)
            .GroupBy(assignment => (assignment.RouteId, MunicipalityId: assignment.MunicipalityId!.Value))
            .ToDictionary(group => group.Key, group => group.Count());
        var currentCustomerIds = customerSnapshots.Select(customer => customer.CustomerId).ToHashSet();
        var mappings = await dbContext.CustomerRouteMappings.AsNoTracking()
            .Where(mapping => mapping.ImportId == mappingImportId && currentCustomerIds.Contains(mapping.CustomerId))
            .OrderBy(mapping => mapping.Weekday)
            .ThenBy(mapping => mapping.SheetName)
            .ThenBy(mapping => mapping.SourceRowNumber)
            .ToListAsync(cancellationToken);
        var mappedCandidates = mappings
            .Select(mapping => new
            {
                Mapping = mapping,
                Route = routes.SingleOrDefault(route => route.Weekday == mapping.Weekday &&
                    CustomerRouteAssignmentsSpreadsheetParser.Normalize(route.Name) == mapping.NormalizedRouteName),
                Customer = customerById.GetValueOrDefault(mapping.CustomerId)
            })
            .Where(item => item.Route is not null && item.Customer is not null)
            .GroupBy(item => (RouteId: item.Route!.Id, MunicipalityId: item.Customer!.MunicipalityId))
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Mapping.SheetName, StringComparer.Ordinal)
                    .ThenBy(item => item.Mapping.SourceRowNumber)
                    .ThenBy(item => item.Mapping.CustomerId)
                    .ToArray());
        var customersByMunicipality = customerSnapshots
            .GroupBy(customer => customer.MunicipalityId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(customer => customer.ExternalCode, StringComparer.Ordinal)
                    .ThenBy(customer => customer.CustomerId)
                    .ToArray());
        var now = DateTime.UtcNow;
        var assignments = new List<RouteCustomerAssignment>();
        foreach (var entryGroup in routeEntries
                     .GroupBy(entry => (entry.RouteId, entry.MunicipalityId))
                     .OrderBy(group => routeById[group.Key.RouteId].Weekday, StringComparer.Ordinal)
                     .ThenBy(group => routeById[group.Key.RouteId].Name, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.RouteId))
        {
            var route = routeById[entryGroup.Key.RouteId];
            var remaining = Math.Max(0, entryGroup.Sum(entry => Math.Max(0, entry.Deliveries)) -
                manualCountByRouteMunicipality.GetValueOrDefault(entryGroup.Key));
            if (remaining == 0) continue;
            if (!usedByWeekday.TryGetValue(route.Weekday, out var used))
            {
                used = [];
                usedByWeekday[route.Weekday] = used;
            }

            var imported = mappedCandidates.GetValueOrDefault(entryGroup.Key, []);
            foreach (var candidate in imported)
            {
                if (remaining == 0 || used.Contains(candidate.Mapping.CustomerId) ||
                    manualPairs.Contains((entryGroup.Key.RouteId, candidate.Mapping.CustomerId))) continue;
                assignments.Add(new RouteCustomerAssignment
                {
                    Id = Guid.NewGuid(), RouteId = entryGroup.Key.RouteId,
                    CustomerId = candidate.Mapping.CustomerId, MunicipalityId = entryGroup.Key.MunicipalityId,
                    Source = RouteCustomerAssignmentSource.Imported, CreatedAt = now, UpdatedAt = now
                });
                used.Add(candidate.Mapping.CustomerId);
                remaining--;
            }

            if (remaining == 0 || !customersByMunicipality.TryGetValue(entryGroup.Key.MunicipalityId, out var candidates))
                continue;
            foreach (var candidate in candidates.Where(candidate => !used.Contains(candidate.CustomerId)))
            {
                if (remaining == 0) break;
                assignments.Add(new RouteCustomerAssignment
                {
                    Id = Guid.NewGuid(), RouteId = entryGroup.Key.RouteId,
                    CustomerId = candidate.CustomerId, MunicipalityId = candidate.MunicipalityId,
                    Source = RouteCustomerAssignmentSource.InferredByMunicipality, CreatedAt = now, UpdatedAt = now
                });
                used.Add(candidate.CustomerId);
                remaining--;
            }
        }
        dbContext.RouteCustomerAssignments.AddRange(assignments);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncInferredAssignmentsAsync(
        Guid routeImportId,
        Guid customerImportId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.RouteCustomerAssignments
            .Where(assignment => assignment.Source == RouteCustomerAssignmentSource.InferredByMunicipality)
            .ToListAsync(cancellationToken);
        dbContext.RouteCustomerAssignments.RemoveRange(existing);

        var routes = await dbContext.Routes.AsNoTracking()
            .Where(route => route.ImportId == routeImportId)
            .Select(route => new { route.Id, route.Name, route.Weekday })
            .ToListAsync(cancellationToken);
        var routeById = routes.ToDictionary(route => route.Id);
        var municipalities = await dbContext.Municipalities.AsNoTracking()
            .Select(municipality => new { municipality.Id, municipality.NormalizedName })
            .ToListAsync(cancellationToken);
        var municipalityIdsByName = municipalities
            .GroupBy(municipality => municipality.NormalizedName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(municipality => municipality.Id).ToArray(),
                StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var routeEntries = await dbContext.RouteEntries.AsNoTracking()
            .Where(entry => entry.Route!.ImportId == routeImportId)
            .Select(entry => new
            {
                entry.Id,
                entry.RouteId,
                entry.MunicipalityId,
                entry.Name,
                entry.Sequence,
                entry.Deliveries
            })
            .ToListAsync(cancellationToken);
        var resolvedEntries = routeEntries
            .SelectMany(entry =>
            {
                if (entry.MunicipalityId.HasValue)
                {
                    return [new RouteMunicipalityEntry(
                        entry.Id, entry.RouteId, entry.Sequence, entry.Deliveries, entry.MunicipalityId.Value)];
                }

                var normalizedName = MunicipalityNameNormalizer.Normalize(entry.Name);
                return municipalityIdsByName.TryGetValue(normalizedName, out var municipalityIds)
                    ? municipalityIds.Select(municipalityId => new RouteMunicipalityEntry(
                        entry.Id, entry.RouteId, entry.Sequence, entry.Deliveries, municipalityId))
                    : [];
            })
            .Distinct()
            .ToArray();
        var municipalityIds = resolvedEntries
            .Select(entry => entry.MunicipalityId)
            .Distinct()
            .ToArray();
        var customerSnapshots = await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(snapshot =>
                snapshot.ImportId == customerImportId &&
                municipalityIds.Contains(snapshot.MunicipalityId))
            .Select(snapshot => new
            {
                snapshot.CustomerId,
                snapshot.MunicipalityId,
                ExternalCode = snapshot.Customer!.ExternalCode
            })
            .ToListAsync(cancellationToken);
        var candidatesByMunicipality = customerSnapshots
            .GroupBy(customer => customer.MunicipalityId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(customer => customer.ExternalCode, StringComparer.Ordinal)
                    .ThenBy(customer => customer.CustomerId)
                    .ToArray());
        var protectedAssignments = await dbContext.RouteCustomerAssignments.AsNoTracking()
            .Where(assignment => assignment.Source != RouteCustomerAssignmentSource.InferredByMunicipality &&
                assignment.Route!.ImportId == routeImportId)
            .Select(assignment => new
            {
                assignment.RouteId,
                assignment.CustomerId,
                assignment.MunicipalityId,
                Weekday = assignment.Route!.Weekday
            })
            .ToListAsync(cancellationToken);
        var protectedPairs = protectedAssignments
            .Select(assignment => (assignment.RouteId, assignment.CustomerId))
            .ToHashSet();
        var usedByWeekday = protectedAssignments
            .GroupBy(assignment => assignment.Weekday, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(assignment => assignment.CustomerId).ToHashSet(),
                StringComparer.Ordinal);
        var protectedCountByRouteMunicipality = protectedAssignments
            .Where(assignment => assignment.MunicipalityId.HasValue)
            .GroupBy(assignment => (assignment.RouteId, MunicipalityId: assignment.MunicipalityId!.Value))
            .ToDictionary(group => group.Key, group => group.Count());
        var generated = new List<RouteCustomerAssignment>();
        foreach (var entry in resolvedEntries
                     .Where(entry => entry.Deliveries > 0 && routeById.ContainsKey(entry.RouteId))
                     .OrderBy(entry => routeById[entry.RouteId].Weekday, StringComparer.Ordinal)
                     .ThenBy(entry => routeById[entry.RouteId].Name, StringComparer.Ordinal)
                     .ThenBy(entry => entry.RouteId)
                     .ThenBy(entry => entry.Sequence)
                     .ThenBy(entry => entry.Id))
        {
            var route = routeById[entry.RouteId];
            if (!candidatesByMunicipality.TryGetValue(entry.MunicipalityId, out var candidates))
                continue;
            if (!usedByWeekday.TryGetValue(route.Weekday, out var used))
            {
                used = [];
                usedByWeekday[route.Weekday] = used;
            }
            var protectedCount = protectedCountByRouteMunicipality
                .GetValueOrDefault((entry.RouteId, entry.MunicipalityId));
            var remainingSlots = Math.Max(0, entry.Deliveries - protectedCount);
            foreach (var candidate in candidates
                         .Where(candidate => !used.Contains(candidate.CustomerId))
                         .Take(remainingSlots))
            {
                if (protectedPairs.Contains((entry.RouteId, candidate.CustomerId)))
                    continue;
                generated.Add(new RouteCustomerAssignment
                {
                    Id = Guid.NewGuid(),
                    RouteId = entry.RouteId,
                    CustomerId = candidate.CustomerId,
                    MunicipalityId = candidate.MunicipalityId,
                    Source = RouteCustomerAssignmentSource.InferredByMunicipality,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                used.Add(candidate.CustomerId);
            }
        }
        dbContext.RouteCustomerAssignments.AddRange(generated);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record RouteMunicipalityEntry(
        Guid Id, Guid RouteId, int Sequence, int Deliveries, Guid MunicipalityId);

    private async Task<Guid?> CurrentImportIdAsync(string sourceCode, CancellationToken cancellationToken) =>
        await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == sourceCode)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
}
