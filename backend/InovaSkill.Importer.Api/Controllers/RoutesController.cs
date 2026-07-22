using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/routes")]
public sealed class RoutesController(ImportDbContext dbContext) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private const int OccupancyPercentScale = 100;
    private const int OccupancyPercentDecimalPlaces = 1;
    private static readonly HashSet<string> SupportedOccupancyLevels =
    [
        "critical",
        "good",
        "medium",
        "idle",
        "unavailable"
    ];

    [HttpGet("occupancy-summary")]
    public async Task<ActionResult> GetOccupancySummary(CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == RouteImportCodes.DataSource)
            .Select(source => source.CurrentImportId == null
                ? null
                : new
                {
                    ImportId = source.CurrentImport!.Id,
                    source.CurrentImport.Version,
                    source.CurrentImport.FileName,
                    source.CurrentImport.FinishedAt
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return Ok(RouteOccupancySummaryResponse.Empty);
        }

        var routes = await dbContext.Routes.AsNoTracking()
            .Where(route => route.ImportId == snapshot.ImportId)
            .Select(route => new
            {
                route.TotalWeightKg,
                CapacityKg = route.VehicleType!.CapacityKg
            })
            .ToListAsync(cancellationToken);

        var routesWithCapacity = routes
            .Where(route => route.CapacityKg is > 0)
            .ToArray();
        var totalWeightKg = routesWithCapacity.Sum(route => route.TotalWeightKg);
        var totalCapacityKg = routesWithCapacity.Sum(route => route.CapacityKg!.Value);
        var occupancyRatePercent = totalCapacityKg > 0
            ? Math.Min(OccupancyPercentScale, Math.Round(
                totalWeightKg / totalCapacityKg * OccupancyPercentScale,
                OccupancyPercentDecimalPlaces,
                MidpointRounding.AwayFromZero))
            : 0m;

        return Ok(new RouteOccupancySummaryResponse(
            occupancyRatePercent,
            totalWeightKg,
            totalCapacityKg,
            routes.Count,
            routesWithCapacity.Length,
            routes.Count - routesWithCapacity.Length,
            new RouteOccupancySnapshotResponse(
                snapshot.ImportId,
                snapshot.Version,
                snapshot.FileName,
                snapshot.FinishedAt)));
    }

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? weekday = null,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? date = null,
        [FromQuery] string? occupancyLevel = null,
        CancellationToken cancellationToken = default)
    {
        var importId = date.HasValue
            ? await ResolveImportAtDateAsync(date.Value, cancellationToken)
            : await dbContext.DataSources.AsNoTracking()
                .Where(source => source.Code == RouteImportCodes.DataSource)
                .Select(source => source.CurrentImportId)
                .SingleOrDefaultAsync(cancellationToken);

        return await ListByImportAsync(
            importId,
            page,
            pageSize,
            weekday,
            search,
            occupancyLevel,
            cancellationToken);
    }

    [HttpGet("/api/route-imports/{importId:guid}/routes")]
    public async Task<ActionResult> ListByImport(
        Guid importId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? weekday = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.RouteImports.AnyAsync(routeImport => routeImport.Id == importId, cancellationToken))
        {
            return NotFound();
        }

        return await ListByImportAsync(importId, page, pageSize, weekday, search, null, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var route = await dbContext.Routes.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Weekday,
                vehicleTypeId = item.VehicleTypeId,
                vehicleType = item.VehicleType!.Name,
                vehicleCapacityKg = item.VehicleType.CapacityKg,
                item.TotalWeightKg,
                item.TotalVolumeM3,
                item.TotalPallets,
                item.WeightOccupancy,
                item.VolumeOccupancy,
                item.PalletOccupancy,
                item.OverallOccupancy,
                occupancyStatus = item.OccupancyStatus.ToString(),
                importId = item.ImportId,
                importVersion = item.Import!.Version,
                importFileName = item.Import.FileName,
                item.CreatedAt,
                entries = item.Entries.OrderBy(entry => entry.Sequence).Select(entry => new
                {
                    entry.Id,
                    entry.Sequence,
                    entry.Name,
                    entry.Deliveries,
                    entry.AveragePerDay,
                    entry.Note
                }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return route is null ? NotFound() : Ok(route);
    }

    private async Task<ActionResult> ListByImportAsync(
        Guid? importId,
        int page,
        int pageSize,
        string? weekday,
        string? search,
        string? occupancyLevel,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var normalizedOccupancyLevel = occupancyLevel?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(normalizedOccupancyLevel) &&
            !SupportedOccupancyLevels.Contains(normalizedOccupancyLevel))
        {
            return BadRequest(new
            {
                message = "Criticidade inválida. Use critical, good, medium, idle ou unavailable."
            });
        }

        if (!importId.HasValue)
        {
            return Ok(new { page, pageSize, total = 0, items = Array.Empty<object>() });
        }

        var query = dbContext.Routes.AsNoTracking()
            .Where(route => route.ImportId == importId.Value);

        if (!string.IsNullOrWhiteSpace(weekday))
        {
            query = query.Where(route => route.Weekday == weekday);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = MunicipalityNameNormalizer.Normalize(search);
            query = query.Where(route =>
                route.Name.ToUpper().Contains(normalizedSearch) ||
                route.Entries.Any(entry => entry.Name.ToUpper().Contains(normalizedSearch)));
        }

        query = normalizedOccupancyLevel switch
        {
            null or "" => query,
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

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(route => route.Weekday)
            .ThenBy(route => route.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(route => new
            {
                route.Id,
                route.Name,
                route.Weekday,
                vehicleTypeId = route.VehicleTypeId,
                vehicleType = route.VehicleType!.Name,
                vehicleCapacityKg = route.VehicleType.CapacityKg,
                route.TotalWeightKg,
                route.TotalVolumeM3,
                route.TotalPallets,
                route.WeightOccupancy,
                route.VolumeOccupancy,
                route.PalletOccupancy,
                route.OverallOccupancy,
                occupancyStatus = route.OccupancyStatus.ToString(),
                importId = route.ImportId,
                importVersion = route.Import!.Version,
                importFileName = route.Import.FileName,
                entryCount = route.Entries.Count,
                totalDeliveries = route.Entries.Sum(entry => entry.Deliveries),
                route.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { page, pageSize, total, items });
    }

    private async Task<Guid?> ResolveImportAtDateAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var exclusiveEnd = RouteSnapshotDatePolicy.GetExclusiveUtcEnd(date);

        return await dbContext.RouteImports.AsNoTracking()
            .Where(routeImport =>
                routeImport.DataSource!.Code == RouteImportCodes.DataSource &&
                routeImport.Status == RouteImportStatus.Completed &&
                routeImport.FinishedAt.HasValue &&
                routeImport.FinishedAt.Value < exclusiveEnd)
            .OrderByDescending(routeImport => routeImport.FinishedAt)
            .ThenByDescending(routeImport => routeImport.Version)
            .Select(routeImport => (Guid?)routeImport.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed record RouteOccupancySummaryResponse(
    decimal OccupancyRatePercent,
    decimal TotalWeightKg,
    decimal TotalCapacityKg,
    int RouteCount,
    int RoutesWithCapacity,
    int RoutesWithoutCapacity,
    RouteOccupancySnapshotResponse? Snapshot)
{
    public static RouteOccupancySummaryResponse Empty { get; } = new(0, 0, 0, 0, 0, 0, null);
}

public sealed record RouteOccupancySnapshotResponse(
    Guid ImportId,
    long Version,
    string FileName,
    DateTime? FinishedAt);
