using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/route-costs")]
public sealed class RouteCostsController(ImportDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] DateOnly? date,
        [FromQuery] string? weekday,
        CancellationToken cancellationToken)
    {
        var normalizedWeekday = NormalizeWeekday(weekday);
        if (weekday is not null && normalizedWeekday is null)
            return BadRequest(new { message = "Dia da semana inválido." });

        var importId = date.HasValue
            ? await ResolveImportAtDateAsync(date.Value, cancellationToken)
            : await ResolveCurrentImportAsync(cancellationToken);
        if (!importId.HasValue)
            return Ok(EmptyResponse());

        var snapshot = await db.RouteCostSnapshots.AsNoTracking()
            .Where(item => item.RouteImportId == importId.Value)
            .Include(item => item.Items).ThenInclude(item => item.TollPassageItems)
            .Include(item => item.Items).ThenInclude(item => item.VehicleType)
            .OrderByDescending(item => item.CalculatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (snapshot is null)
            return Ok(EmptyResponse(importId));

        var items = snapshot.Items
            .Where(item => normalizedWeekday is null || item.Weekday == normalizedWeekday)
            .OrderBy(item => item.Weekday, StringComparer.Ordinal)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .ToArray();
        var actual = BuildScenario(items.Where(item => item.Scenario == RouteCostScenarios.Actual));
        var optimizedItems = items.Where(item => item.Scenario == RouteCostScenarios.Optimized).ToArray();
        var resultIds = optimizedItems
            .Where(item => item.OptimizationResultId.HasValue)
            .Select(item => item.OptimizationResultId!.Value)
            .Distinct()
            .ToArray();
        var optimizationResults = await db.DailyRouteOptimizationResults.AsNoTracking()
            .Where(item => item.RouteImportId == importId.Value &&
                (normalizedWeekday == null || item.Weekday == normalizedWeekday))
            .OrderBy(item => item.Weekday)
            .Select(item => new
            {
                item.Id,
                item.Weekday,
                item.Status,
                item.Reason,
                item.CreatedAt,
                hasConsolidatedCosts = resultIds.Contains(item.Id)
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            snapshotId = snapshot.Id,
            routeImportId = snapshot.RouteImportId,
            snapshot.CalculatedAt,
            snapshot.DieselPricePerLiter,
            snapshot.TollCatalogVersion,
            snapshot.TollEffectiveFrom,
            actual,
            optimized = optimizedItems.Length == 0 ? null : BuildScenario(optimizedItems),
            optimizationResults
        });
    }

    [HttpGet("/api/routes/{routeId:guid}/cost")]
    public async Task<ActionResult> GetRouteCost(Guid routeId, CancellationToken cancellationToken)
    {
        var route = await db.Routes.AsNoTracking()
            .Where(item => item.Id == routeId)
            .Select(item => new { item.Id, item.ImportId })
            .SingleOrDefaultAsync(cancellationToken);
        if (route is null) return NotFound();

        var snapshot = await db.RouteCostSnapshots.AsNoTracking()
            .Where(item => item.RouteImportId == route.ImportId)
            .Include(item => item.Items.Where(cost =>
                cost.Scenario == RouteCostScenarios.Actual && cost.RouteId == routeId))
            .ThenInclude(item => item.TollPassageItems)
            .Include(item => item.Items.Where(cost =>
                cost.Scenario == RouteCostScenarios.Actual && cost.RouteId == routeId))
            .ThenInclude(item => item.VehicleType)
            .OrderByDescending(item => item.CalculatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var item = snapshot?.Items.SingleOrDefault();
        if (snapshot is null || item is null)
            return NotFound(new { message = "O custo consolidado desta rota ainda não está disponível." });

        return Ok(new
        {
            snapshotId = snapshot.Id,
            routeImportId = snapshot.RouteImportId,
            snapshot.CalculatedAt,
            snapshot.DieselPricePerLiter,
            snapshot.TollCatalogVersion,
            snapshot.TollEffectiveFrom,
            item = ToItem(item)
        });
    }

    private static object BuildScenario(IEnumerable<RouteCostItem> source)
    {
        var items = source.ToArray();
        var available = items.Where(item => item.IsAvailable).ToArray();
        var hasAvailable = available.Length > 0;
        return new
        {
            pathBasis = ResolvePathBasis(items),
            items = items.Select(ToItem).ToArray(),
            totals = new
            {
                itemCount = items.Length,
                availableItemCount = available.Length,
                unavailableItemCount = items.Length - available.Length,
                distanceMeters = available.Sum(item => item.DistanceMeters ?? 0),
                durationSeconds = available.Sum(item => item.DurationSeconds ?? 0),
                minimumFuelLiters = hasAvailable ? available.Sum(item => item.MinimumFuelLiters ?? 0) : (decimal?)null,
                maximumFuelLiters = hasAvailable ? available.Sum(item => item.MaximumFuelLiters ?? 0) : (decimal?)null,
                minimumFuelCost = hasAvailable ? available.Sum(item => item.MinimumFuelCost ?? 0) : (decimal?)null,
                maximumFuelCost = hasAvailable ? available.Sum(item => item.MaximumFuelCost ?? 0) : (decimal?)null,
                tollCost = available.Sum(item => item.TollCost),
                tollPassages = available.Sum(item => item.TollPassages),
                minimumTotalCost = hasAvailable ? available.Sum(item => item.MinimumTotalCost ?? 0) : (decimal?)null,
                maximumTotalCost = hasAvailable ? available.Sum(item => item.MaximumTotalCost ?? 0) : (decimal?)null
            }
        };
    }

    private static string? ResolvePathBasis(IReadOnlyCollection<RouteCostItem> items)
    {
        var bases = items
            .Select(item => item.PathBasis)
            .Where(pathBasis => !string.IsNullOrWhiteSpace(pathBasis))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return bases.Length switch
        {
            0 => null,
            1 => bases[0],
            _ => RouteCostPathBases.Mixed
        };
    }

    private static object ToItem(RouteCostItem item) => new
    {
        item.Id,
        item.RouteId,
        item.OptimizationResultId,
        item.OptimizationVehicleId,
        item.VehicleTypeId,
        vehicleType = item.VehicleType?.Name,
        item.Label,
        item.Weekday,
        item.PathBasis,
        item.IsAvailable,
        item.UnavailableReason,
        item.DistanceMeters,
        item.DurationSeconds,
        item.MinimumFuelLiters,
        item.MaximumFuelLiters,
        item.MinimumFuelCost,
        item.MaximumFuelCost,
        item.TollCost,
        item.TollPassages,
        item.MinimumTotalCost,
        item.MaximumTotalCost,
        tolls = item.TollPassageItems.OrderBy(toll => toll.TollPlazaName).Select(toll => new
        {
            toll.TollPlazaCode,
            toll.TollPlazaName,
            toll.OperatorName,
            toll.Highway,
            toll.Kilometer,
            toll.AxleCount,
            toll.Passages,
            toll.AutomaticUnitTariff,
            toll.TotalCost
        })
    };

    private static string? NormalizeWeekday(string? weekday)
    {
        if (string.IsNullOrWhiteSpace(weekday)) return null;
        var normalized = weekday.Trim().ToUpperInvariant();
        return normalized is "MONDAY" or "TUESDAY" or "WEDNESDAY" or "THURSDAY" or "FRIDAY" or
            "SATURDAY" or "SUNDAY" ? normalized : null;
    }

    private async Task<Guid?> ResolveCurrentImportAsync(CancellationToken cancellationToken) =>
        await db.DataSources.AsNoTracking()
            .Where(source => source.Code == RouteImportCodes.DataSource)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Guid?> ResolveImportAtDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var exclusiveEnd = RouteSnapshotDatePolicy.GetExclusiveUtcEnd(date);
        return await db.RouteImports.AsNoTracking()
            .Where(routeImport => routeImport.DataSource!.Code == RouteImportCodes.DataSource &&
                routeImport.Status == RouteImportStatus.Completed && routeImport.FinishedAt.HasValue &&
                routeImport.FinishedAt.Value < exclusiveEnd)
            .OrderByDescending(routeImport => routeImport.FinishedAt)
            .ThenByDescending(routeImport => routeImport.Version)
            .Select(routeImport => (Guid?)routeImport.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static object EmptyResponse(Guid? routeImportId = null) => new
    {
        snapshotId = (Guid?)null,
        routeImportId,
        calculatedAt = (DateTime?)null,
        dieselPricePerLiter = (decimal?)null,
        tollCatalogVersion = (string?)null,
        tollEffectiveFrom = (DateOnly?)null,
        actual = BuildScenario([]),
        optimized = (object?)null,
        optimizationResults = Array.Empty<object>()
    };
}
