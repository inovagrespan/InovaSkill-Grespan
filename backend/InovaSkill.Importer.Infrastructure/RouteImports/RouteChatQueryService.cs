using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class RouteChatQueryService(ImportDbContext dbContext) : IRouteChatQueryService
{
    private const int OccupancyPercentScale = 100;
    private const int OccupancyPercentDecimalPlaces = 1;
    private const string InferredByMunicipalityRelationshipType = "InferredByMunicipality";
    private const string InferredByMunicipalityRelationshipDescription =
        "Enquanto não existir vínculo manual cliente-rota, o relacionamento é inferido pelo município do cliente e pelas cidades reconhecidas da rota.";

    public async Task<IReadOnlyList<RouteChatSummaryDto>> SearchRoutesAsync(
        string searchTerm,
        int limit,
        CancellationToken cancellationToken)
    {
        var importId = await GetCurrentRouteImportIdAsync(cancellationToken);
        if (!importId.HasValue) return [];

        var normalizedSearch = MunicipalityNameNormalizer.Normalize(searchTerm);

        var routes = await dbContext.Routes.AsNoTracking()
            .Where(route => route.ImportId == importId.Value)
            .Where(route =>
                route.Name.ToUpper().Contains(normalizedSearch) ||
                route.Entries.Any(entry => entry.Name.ToUpper().Contains(normalizedSearch)))
            .OrderBy(route => route.Name)
            .Take(limit)
            .Select(route => new
            {
                route.Id,
                route.Name,
                route.OverallOccupancy
            })
            .ToListAsync(cancellationToken);

        return routes
            .Select(route => new RouteChatSummaryDto(
                route.Id,
                route.Name,
                RouteOccupancyLevelPolicy.Label(RouteOccupancyLevelPolicy.Classify(route.OverallOccupancy)),
                ToOccupancyPercentage(route.OverallOccupancy)))
            .ToList();
    }

    public async Task<RouteChatDetailsDto?> GetRouteDetailsAsync(
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var route = await dbContext.Routes.AsNoTracking()
            .Where(item => item.Id == routeId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.OverallOccupancy,
                item.CreatedAt,
                CityCount = item.Entries.Count,
                DeliveryCount = item.Entries.Sum(entry => entry.Deliveries),
                PotentialCustomerCount = item.CustomerAssignments.Count
            })
            .SingleOrDefaultAsync(cancellationToken);

        return route is null
            ? null
            : new RouteChatDetailsDto(
                route.Id,
                route.Name,
                RouteOccupancyLevelPolicy.Label(RouteOccupancyLevelPolicy.Classify(route.OverallOccupancy)),
                ToOccupancyPercentage(route.OverallOccupancy),
                route.CityCount,
                route.DeliveryCount,
                route.PotentialCustomerCount,
                route.CreatedAt);
    }

    public async Task<IReadOnlyList<RouteChatCriticalDto>> GetCriticalRoutesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var importId = await GetCurrentRouteImportIdAsync(cancellationToken);
        if (!importId.HasValue) return [];

        var routes = await dbContext.Routes.AsNoTracking()
            .Where(route =>
                route.ImportId == importId.Value &&
                route.OccupancyStatus == RouteOccupancyStatus.Calculated &&
                route.OverallOccupancy > RouteOccupancyLevelPolicy.CriticalMinimumExclusive)
            .OrderByDescending(route => route.OverallOccupancy)
            .ThenBy(route => route.Name)
            .Take(limit)
            .Select(route => new
            {
                route.Id,
                route.Name,
                route.OverallOccupancy
            })
            .ToListAsync(cancellationToken);

        return routes
            .Select(route => new RouteChatCriticalDto(
                route.Id,
                route.Name,
                RouteOccupancyLevelPolicy.Label("critical"),
                ToOccupancyPercentage(route.OverallOccupancy),
                "Ocupação acima do limite saudável."))
            .ToList();
    }

    public async Task<IReadOnlyList<RouteChatSummaryDto>> ListRoutesByOccupancyAsync(
        RouteChatOccupancyQuery occupancyQuery,
        CancellationToken cancellationToken)
    {
        var importId = await GetCurrentRouteImportIdAsync(cancellationToken);
        if (!importId.HasValue) return [];

        var query = dbContext.Routes.AsNoTracking()
            .Where(route => route.ImportId == importId.Value);

        query = occupancyQuery.OccupancyLevel switch
        {
            "critical" => query.Where(route =>
                route.OverallOccupancy > RouteOccupancyLevelPolicy.CriticalMinimumExclusive),
            "good" => query.Where(route =>
                route.OverallOccupancy >= RouteOccupancyLevelPolicy.GoodMinimum &&
                route.OverallOccupancy <= RouteOccupancyLevelPolicy.CriticalMinimumExclusive),
            "medium" => query.Where(route =>
                route.OverallOccupancy >= RouteOccupancyLevelPolicy.MediumMinimum &&
                route.OverallOccupancy < RouteOccupancyLevelPolicy.GoodMinimum),
            "idle" => query.Where(route =>
                route.OverallOccupancy != null &&
                route.OverallOccupancy < RouteOccupancyLevelPolicy.MediumMinimum),
            "unavailable" => query.Where(route => route.OverallOccupancy == null),
            _ => query
        };

        if (occupancyQuery.MinimumOccupancyPercentage.HasValue)
        {
            var minimum = occupancyQuery.MinimumOccupancyPercentage.Value / OccupancyPercentScale;
            query = query.Where(route => route.OverallOccupancy >= minimum);
        }

        if (occupancyQuery.MaximumOccupancyPercentage.HasValue)
        {
            var maximum = occupancyQuery.MaximumOccupancyPercentage.Value / OccupancyPercentScale;
            query = query.Where(route => route.OverallOccupancy <= maximum);
        }

        query = occupancyQuery.SortDirection == "asc"
            ? query.OrderBy(route => route.OverallOccupancy).ThenBy(route => route.Name)
            : query.OrderByDescending(route => route.OverallOccupancy).ThenBy(route => route.Name);

        var routes = await query
            .Take(occupancyQuery.Limit)
            .Select(route => new
            {
                route.Id,
                route.Name,
                route.OverallOccupancy
            })
            .ToListAsync(cancellationToken);

        return routes
            .Select(route => new RouteChatSummaryDto(
                route.Id,
                route.Name,
                RouteOccupancyLevelPolicy.Label(RouteOccupancyLevelPolicy.Classify(route.OverallOccupancy)),
                ToOccupancyPercentage(route.OverallOccupancy)))
            .ToList();
    }

    public async Task<RouteChatCitiesDto?> GetRouteCitiesAsync(
        Guid routeId,
        int limit,
        CancellationToken cancellationToken)
    {
        var route = await dbContext.Routes.AsNoTracking()
            .Where(item => item.Id == routeId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                Cities = item.Entries
                    .OrderBy(entry => entry.Sequence)
                    .Take(limit)
                    .Select(entry => new RouteChatCityDto(
                        entry.Municipality != null ? entry.Municipality.Name : entry.Name,
                        entry.Municipality != null ? entry.Municipality.StateCode : null))
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return route is null
            ? null
            : new RouteChatCitiesDto(route.Id, route.Name, route.Cities);
    }

    public async Task<RouteChatRouteCustomersDto?> GetRouteCustomersAsync(
        Guid routeId,
        int limit,
        CancellationToken cancellationToken)
    {
        var route = await dbContext.Routes.AsNoTracking()
            .Where(item => item.Id == routeId)
            .Select(item => new
            {
                item.Id,
                item.Name
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (route is null)
        {
            return null;
        }

        var currentCustomerImportId = await GetCurrentCustomerImportIdAsync(cancellationToken);
        if (!currentCustomerImportId.HasValue)
        {
            return new RouteChatRouteCustomersDto(
                route.Id,
                route.Name,
                InferredByMunicipalityRelationshipType,
                InferredByMunicipalityRelationshipDescription,
                []);
        }

        var customers = await dbContext.RouteCustomerAssignments.AsNoTracking()
            .Where(assignment => assignment.RouteId == route.Id)
            .Join(
                dbContext.CustomerSnapshots.AsNoTracking()
                    .Where(snapshot => snapshot.ImportId == currentCustomerImportId.Value),
                assignment => assignment.CustomerId,
                snapshot => snapshot.CustomerId,
                (assignment, snapshot) => new { assignment, snapshot })
            .OrderBy(item => item.snapshot.Municipality!.Name)
            .ThenBy(item => item.snapshot.Customer!.ExternalCode)
            .ThenBy(item => item.snapshot.Customer!.BranchCode)
            .Take(limit)
            .Select(item => new RouteChatCustomerDto(
                item.snapshot.CustomerId,
                item.snapshot.Customer!.ExternalCode,
                item.snapshot.Customer.BranchCode,
                item.snapshot.LegalName,
                item.snapshot.TradeName,
                item.snapshot.Municipality!.Name,
                item.snapshot.Municipality.StateCode,
                item.snapshot.CustomerType))
            .ToListAsync(cancellationToken);

        return new RouteChatRouteCustomersDto(
            route.Id,
            route.Name,
            InferredByMunicipalityRelationshipType,
            InferredByMunicipalityRelationshipDescription,
            customers);
    }

    private async Task<Guid?> GetCurrentRouteImportIdAsync(CancellationToken cancellationToken) =>
        await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == RouteImportCodes.DataSource)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Guid?> GetCurrentCustomerImportIdAsync(CancellationToken cancellationToken) =>
        await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == CustomerImportCodes.DataSource)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);

    private static decimal? ToOccupancyPercentage(decimal? occupancy) =>
        occupancy.HasValue
            ? Math.Round(
                occupancy.Value * OccupancyPercentScale,
                OccupancyPercentDecimalPlaces,
                MidpointRounding.AwayFromZero)
            : null;

}
