using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class RouteChatQueryService(ImportDbContext dbContext) : IRouteChatQueryService
{
    private const int OccupancyPercentScale = 100;
    private const int OccupancyPercentDecimalPlaces = 1;
    private const decimal MetersPerKilometer = 1000m;
    private const decimal SecondsPerMinute = 60m;
    private const int DistanceDecimalPlaces = 1;
    private const int DurationDecimalPlaces = 1;
    private const string InferredByMunicipalityRelationshipType = "InferredByMunicipality";
    private const string InferredByMunicipalityRelationshipDescription =
        "Enquanto não existir vínculo manual cliente-rota, o relacionamento é inferido pelo município do cliente e pelas cidades reconhecidas da rota.";
    private const string ImportedRelationshipDescription =
        "Relacionamento oficial importado da planilha de vínculos entre clientes, dias e rotas.";

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
                route.Weekday,
                VehicleType = route.VehicleType!.Name,
                route.TotalWeightKg,
                route.TotalVolumeM3,
                route.TotalPallets,
                route.OverallOccupancy,
                route.WeightOccupancy,
                route.VolumeOccupancy,
                route.PalletOccupancy
            })
            .ToListAsync(cancellationToken);

        return routes
            .Select(route => new RouteChatSummaryDto(
                route.Id,
                route.Name,
                route.Weekday,
                route.VehicleType,
                route.TotalWeightKg,
                route.TotalVolumeM3,
                route.TotalPallets,
                RouteOccupancyLevelPolicy.Label(RouteOccupancyLevelPolicy.Classify(route.OverallOccupancy)),
                ToOccupancyPercentage(route.OverallOccupancy),
                ToOccupancyPercentage(route.WeightOccupancy),
                ToOccupancyPercentage(route.VolumeOccupancy),
                ToOccupancyPercentage(route.PalletOccupancy)))
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
                item.Weekday,
                VehicleType = item.VehicleType!.Name,
                VehicleCapacityKg = item.VehicleType.CapacityKg,
                VehicleCapacityVolumeM3 = item.VehicleType.CapacityVolumeM3,
                VehicleCapacityPallets = item.VehicleType.CapacityPallets,
                item.TotalWeightKg,
                item.TotalVolumeM3,
                item.TotalPallets,
                item.OverallOccupancy,
                item.WeightOccupancy,
                item.VolumeOccupancy,
                item.PalletOccupancy,
                item.CreatedAt,
                CityCount = item.Entries.Count,
                DeliveryCount = item.Entries.Sum(entry => entry.Deliveries),
                PotentialCustomerCount = item.CustomerAssignments.Count(assignment => assignment.Customer!.IsActive)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return route is null
            ? null
            : new RouteChatDetailsDto(
                route.Id,
                route.Name,
                route.Weekday,
                route.VehicleType,
                route.VehicleCapacityKg,
                route.VehicleCapacityVolumeM3,
                route.VehicleCapacityPallets,
                route.TotalWeightKg,
                route.TotalVolumeM3,
                route.TotalPallets,
                RouteOccupancyLevelPolicy.Label(RouteOccupancyLevelPolicy.Classify(route.OverallOccupancy)),
                ToOccupancyPercentage(route.OverallOccupancy),
                ToOccupancyPercentage(route.WeightOccupancy),
                ToOccupancyPercentage(route.VolumeOccupancy),
                ToOccupancyPercentage(route.PalletOccupancy),
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
                route.Weekday,
                VehicleType = route.VehicleType!.Name,
                route.TotalWeightKg,
                route.TotalVolumeM3,
                route.TotalPallets,
                route.OverallOccupancy,
                route.WeightOccupancy,
                route.VolumeOccupancy,
                route.PalletOccupancy
            })
            .ToListAsync(cancellationToken);

        return routes
            .Select(route => new RouteChatCriticalDto(
                route.Id,
                route.Name,
                route.Weekday,
                route.VehicleType,
                route.TotalWeightKg,
                route.TotalVolumeM3,
                route.TotalPallets,
                RouteOccupancyLevelPolicy.Label("critical"),
                ToOccupancyPercentage(route.OverallOccupancy),
                ToOccupancyPercentage(route.WeightOccupancy),
                ToOccupancyPercentage(route.VolumeOccupancy),
                ToOccupancyPercentage(route.PalletOccupancy),
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
                route.Weekday,
                VehicleType = route.VehicleType!.Name,
                route.TotalWeightKg,
                route.TotalVolumeM3,
                route.TotalPallets,
                route.OverallOccupancy,
                route.WeightOccupancy,
                route.VolumeOccupancy,
                route.PalletOccupancy
            })
            .ToListAsync(cancellationToken);

        return routes
            .Select(route => new RouteChatSummaryDto(
                route.Id,
                route.Name,
                route.Weekday,
                route.VehicleType,
                route.TotalWeightKg,
                route.TotalVolumeM3,
                route.TotalPallets,
                RouteOccupancyLevelPolicy.Label(RouteOccupancyLevelPolicy.Classify(route.OverallOccupancy)),
                ToOccupancyPercentage(route.OverallOccupancy),
                ToOccupancyPercentage(route.WeightOccupancy),
                ToOccupancyPercentage(route.VolumeOccupancy),
                ToOccupancyPercentage(route.PalletOccupancy)))
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
                        entry.Sequence,
                        entry.Municipality != null ? entry.Municipality.Name : entry.Name,
                        entry.Municipality != null ? entry.Municipality.StateCode : null,
                        entry.Deliveries,
                        entry.AveragePerDay,
                        entry.Note))
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
            .Where(item => item.snapshot.Customer!.IsActive)
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

        var imported = await dbContext.DataSources.AsNoTracking().AnyAsync(source =>
            source.Code == CustomerRouteAssignmentImportCodes.DataSource && source.CurrentImportId != null,
            cancellationToken);
        return new RouteChatRouteCustomersDto(
            route.Id,
            route.Name,
            imported ? RouteCustomerAssignmentSource.Imported.ToString() : InferredByMunicipalityRelationshipType,
            imported ? ImportedRelationshipDescription : InferredByMunicipalityRelationshipDescription,
            customers);
    }

    public async Task<RouteChatOperationalAnalysisDto?> GetRouteOperationalAnalysisAsync(
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var route = await GetRouteDetailsAsync(routeId, cancellationToken);
        if (route is null) return null;

        var importId = await GetCurrentRouteImportIdAsync(cancellationToken);
        if (!importId.HasValue || !await dbContext.Routes.AsNoTracking().AnyAsync(
                item => item.Id == routeId && item.ImportId == importId.Value,
                cancellationToken))
            return null;
        var snapshot = await dbContext.RouteCostSnapshots.AsNoTracking()
            .Where(item => item.RouteImportId == importId.Value)
            .OrderByDescending(item => item.CalculatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var costItem = snapshot is null
            ? null
            : await dbContext.RouteCostItems.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.SnapshotId == snapshot.Id &&
                            item.Scenario == RouteCostScenarios.Actual &&
                            item.RouteId == routeId,
                    cancellationToken);
        IReadOnlyList<RouteCostItem> dailyCostItems = snapshot is null
            ? []
            : await dbContext.RouteCostItems.AsNoTracking()
                .Where(item => item.SnapshotId == snapshot.Id && item.Weekday == route.Weekday)
                .ToListAsync(cancellationToken);
        var dailyCosts = snapshot is null
            ? null
            : new RouteChatDailyCostComparisonDto(
                snapshot.CalculatedAt,
                snapshot.DieselPricePerLiter,
                snapshot.TollCatalogVersion,
                SummarizeCosts(dailyCostItems, RouteCostScenarios.Actual),
                SummarizeCosts(dailyCostItems, RouteCostScenarios.Optimized));
        var optimization = await GetDailyRouteOptimizationAsync(route.Weekday, cancellationToken);
        var costsAreStale = snapshot is not null && importId.HasValue &&
            (await HasPendingCostRefreshAsync(importId.Value, snapshot.CalculatedAt, cancellationToken) ||
             optimization?.CalculatedAt > snapshot.CalculatedAt);
        var hasValidOptimization = optimization?.Status is
            DailyRouteOptimizationStatuses.Optimized or DailyRouteOptimizationStatuses.NoImprovement;
        var optimizedCostsAvailable = dailyCosts?.Optimized?.AvailableItemCount > 0;
        return new RouteChatOperationalAnalysisDto(
            route,
            costItem is null ? null : ToCostDto(costItem, snapshot!),
            dailyCosts,
            optimization,
            "Comparação global do dia; os veículos propostos não substituem uma rota atual em relação 1:1.",
            costsAreStale
                ? "Dados insuficientes: os custos estão desatualizados e aguardam nova consolidação."
                : costItem is null || !costItem.IsAvailable
                ? $"Dados insuficientes: {costItem?.UnavailableReason ?? "custo consolidado da rota não está disponível no snapshot atual."}"
                : !hasValidOptimization
                    ? "Dados insuficientes: não há cenário válido persistido pelo otimizador para o dia da rota."
                    : optimization?.Status == DailyRouteOptimizationStatuses.Optimized && !optimizedCostsAvailable
                        ? "Dados insuficientes: o cenário otimizado ainda não possui custos consolidados."
                    : null);
    }

    public async Task<RouteChatDailyOptimizationDto?> GetDailyRouteOptimizationAsync(
        string weekday,
        CancellationToken cancellationToken)
    {
        var importId = await GetCurrentRouteImportIdAsync(cancellationToken);
        if (!importId.HasValue) return null;

        var normalizedWeekday = weekday.Trim().ToUpperInvariant();
        var result = await dbContext.DailyRouteOptimizationResults.AsNoTracking()
            .Include(item => item.Vehicles)
                .ThenInclude(vehicle => vehicle.VehicleType)
            .Include(item => item.Vehicles)
                .ThenInclude(vehicle => vehicle.SourceRoute)
            .Include(item => item.Vehicles)
                .ThenInclude(vehicle => vehicle.Stops)
                    .ThenInclude(stop => stop.Municipality)
            .Include(item => item.Vehicles)
                .ThenInclude(vehicle => vehicle.Stops)
                    .ThenInclude(stop => stop.Customer)
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(
                item => item.RouteImportId == importId.Value && item.Weekday == normalizedWeekday,
                cancellationToken);
        if (result is null) return null;

        var proposedVehicles = result.Vehicles
            .OrderBy(vehicle => vehicle.Sequence)
            .Select(vehicle => new RouteChatOptimizationVehicleDto(
                vehicle.Sequence,
                vehicle.SourceRouteId,
                vehicle.SourceRoute?.Name,
                vehicle.VehicleType?.Name ?? "Não informado",
                vehicle.IsAdditional,
                vehicle.IsIdle,
                vehicle.CapacityKg,
                vehicle.LoadKg,
                ToOccupancyPercentage(vehicle.Occupancy) ?? 0m,
                ToKilometers(vehicle.DistanceMeters),
                ToMinutes(vehicle.DurationSeconds),
                vehicle.Stops
                    .OrderBy(stop => stop.Sequence)
                    .Select(stop => new RouteChatOptimizationStopDto(
                        stop.Sequence,
                        stop.Customer is null
                            ? stop.Municipality?.Name ?? "Não informado"
                            : $"{stop.Customer.ExternalCode} — {stop.Municipality?.Name ?? "município não informado"}",
                        stop.Municipality?.StateCode ?? string.Empty,
                        stop.WeightKg))
                    .ToList()))
            .ToList();

        return new RouteChatDailyOptimizationDto(
            result.Id,
            result.Weekday,
            result.Status,
            result.Reason,
            result.CreatedAt,
            "O resultado otimiza o conjunto de rotas do dia; não há correspondência 1:1 entre rota atual e veículo proposto.",
            new RouteChatOptimizationMetricsDto(
                ToKilometers(result.CurrentDistanceMeters),
                ToMinutes(result.CurrentDurationSeconds),
                result.CurrentVehicleCount,
                result.TotalWeightKg),
            new RouteChatOptimizationMetricsDto(
                ToKilometers(result.ProposedDistanceMeters),
                ToMinutes(result.ProposedDurationSeconds),
                result.ProposedVehicleCount,
                result.TotalWeightKg),
            proposedVehicles,
            result.Issues
                .OrderBy(issue => issue.Code)
                .ThenBy(issue => issue.Message)
                .Select(issue => new RouteChatOptimizationIssueDto(issue.Code, issue.Message, issue.CanResolve))
                .ToList());
    }

    public async Task<RouteChatCostListDto> ListRouteCostsAsync(
        RouteChatCostQuery query,
        CancellationToken cancellationToken)
    {
        var importId = await GetCurrentRouteImportIdAsync(cancellationToken);
        if (!importId.HasValue)
            return UnavailableCosts(query, "Dados insuficientes: não há importação de rotas publicada.");

        var snapshot = await dbContext.RouteCostSnapshots.AsNoTracking()
            .Where(item => item.RouteImportId == importId.Value)
            .OrderByDescending(item => item.CalculatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (snapshot is null)
            return UnavailableCosts(query, "Dados insuficientes: não há snapshot consolidado de custos.");
        if (await HasPendingCostRefreshAsync(importId.Value, snapshot.CalculatedAt, cancellationToken))
            return UnavailableCosts(query, "Dados insuficientes: os custos estão desatualizados e aguardam nova consolidação.");

        var weekday = query.Weekday?.Trim().ToUpperInvariant();
        var itemsQuery = dbContext.RouteCostItems.AsNoTracking()
            .Where(item => item.SnapshotId == snapshot.Id && item.Scenario == query.Scenario);
        if (!string.IsNullOrWhiteSpace(weekday))
            itemsQuery = itemsQuery.Where(item => item.Weekday == weekday);

        var allItems = await itemsQuery.ToListAsync(cancellationToken);

        itemsQuery = (query.SortBy, query.SortDirection) switch
        {
            ("fuelCost", "asc") => itemsQuery.OrderByDescending(item => item.IsAvailable)
                .ThenBy(item => item.MaximumFuelCost).ThenBy(item => item.Label),
            ("fuelCost", _) => itemsQuery.OrderByDescending(item => item.IsAvailable)
                .ThenByDescending(item => item.MaximumFuelCost).ThenBy(item => item.Label),
            ("tollCost", "asc") => itemsQuery.OrderByDescending(item => item.IsAvailable)
                .ThenBy(item => item.TollCost).ThenBy(item => item.Label),
            ("tollCost", _) => itemsQuery.OrderByDescending(item => item.IsAvailable)
                .ThenByDescending(item => item.TollCost).ThenBy(item => item.Label),
            ("totalCost", "asc") => itemsQuery.OrderByDescending(item => item.IsAvailable)
                .ThenBy(item => item.MaximumTotalCost).ThenBy(item => item.Label),
            _ => itemsQuery.OrderByDescending(item => item.IsAvailable)
                .ThenByDescending(item => item.MaximumTotalCost).ThenBy(item => item.Label)
        };

        var items = await itemsQuery.Take(query.Limit).ToListAsync(cancellationToken);
        var hasAvailableItem = items.Any(item => item.IsAvailable);
        return new RouteChatCostListDto(
            DateOnly.FromDateTime(snapshot.CalculatedAt),
            weekday,
            hasAvailableItem ? "Available" : "Unavailable",
            hasAvailableItem
                ? null
                : "Dados insuficientes: nenhuma rota possui custo disponível para o período informado.",
            SummarizeCosts(allItems, query.Scenario),
            items.Select(item => ToCostDto(item, snapshot)).ToList());
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

    private Task<bool> HasPendingCostRefreshAsync(
        Guid routeImportId,
        DateTime calculatedAt,
        CancellationToken cancellationToken) =>
        dbContext.JobExecutions.AsNoTracking().AnyAsync(job =>
            job.JobType == OperationalJobCodes.RouteCostConsolidation &&
            job.RelatedEntityId == routeImportId &&
            job.CreatedAt > calculatedAt &&
            (job.Status == JobExecutionStatus.Queued ||
             job.Status == JobExecutionStatus.Processing ||
             job.Status == JobExecutionStatus.Retrying),
            cancellationToken);

    private static decimal? ToOccupancyPercentage(decimal? occupancy) =>
        occupancy.HasValue
            ? Math.Round(
                occupancy.Value * OccupancyPercentScale,
                OccupancyPercentDecimalPlaces,
                MidpointRounding.AwayFromZero)
            : null;

    private static decimal ToKilometers(decimal meters) =>
        Math.Round(meters / MetersPerKilometer, DistanceDecimalPlaces, MidpointRounding.AwayFromZero);

    private static decimal ToMinutes(decimal seconds) =>
        Math.Round(seconds / SecondsPerMinute, DurationDecimalPlaces, MidpointRounding.AwayFromZero);

    private static RouteChatCostListDto UnavailableCosts(RouteChatCostQuery query, string message) =>
        new(null, query.Weekday, "Unavailable", message, null, []);

    private static RouteChatCostDto ToCostDto(RouteCostItem item, RouteCostSnapshot snapshot) =>
        new(
            item.RouteId,
            item.Label,
            item.Scenario,
            item.PathBasis,
            item.IsAvailable ? "Available" : "Unavailable",
            item.UnavailableReason,
            item.DistanceMeters.HasValue ? ToKilometers(item.DistanceMeters.Value) : null,
            item.DurationSeconds.HasValue ? ToMinutes(item.DurationSeconds.Value) : null,
            item.MinimumFuelLiters,
            item.MaximumFuelLiters,
            item.MinimumFuelCost,
            item.MaximumFuelCost,
            item.TollCost,
            item.TollPassages,
            item.MinimumTotalCost,
            item.MaximumTotalCost,
            snapshot.DieselPricePerLiter,
            snapshot.TollCatalogVersion,
            snapshot.TollEffectiveFrom,
            snapshot.CalculatedAt);

    private static RouteChatScenarioCostSummaryDto? SummarizeCosts(
        IReadOnlyCollection<RouteCostItem> items,
        string scenario)
    {
        var scenarioItems = items.Where(item => item.Scenario == scenario).ToArray();
        if (scenarioItems.Length == 0) return null;

        var available = scenarioItems.Where(item => item.IsAvailable).ToArray();
        var hasAvailable = available.Length > 0;
        var pathBases = scenarioItems.Select(item => item.PathBasis).Distinct(StringComparer.Ordinal).ToArray();
        return new RouteChatScenarioCostSummaryDto(
            scenario,
            pathBases.Length == 1 ? pathBases[0] : "MIXED",
            available.Length,
            scenarioItems.Length - available.Length,
            hasAvailable ? ToKilometers(available.Sum(item => item.DistanceMeters ?? 0m)) : null,
            hasAvailable ? available.Sum(item => item.MinimumFuelCost ?? 0m) : null,
            hasAvailable ? available.Sum(item => item.MaximumFuelCost ?? 0m) : null,
            hasAvailable ? available.Sum(item => item.TollCost) : null,
            hasAvailable ? available.Sum(item => item.MinimumTotalCost ?? 0m) : null,
            hasAvailable ? available.Sum(item => item.MaximumTotalCost ?? 0m) : null);
    }

}
