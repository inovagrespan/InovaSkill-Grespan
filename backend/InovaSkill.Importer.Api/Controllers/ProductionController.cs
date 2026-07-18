using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/production")]
public sealed class ProductionController(ImportDbContext dbContext) : ControllerBase
{
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 100;

    [HttpGet("summary")]
    public async Task<ActionResult> Summary(CancellationToken cancellationToken)
    {
        var dailyImportId = await CurrentDailyImportIdAsync(cancellationToken);
        if (!dailyImportId.HasValue)
            return Ok(new { lastDailyDate = (string?)null, lastProduction = 0m, lastOutbound = 0m, operationalBalance = 0m, totalProductionMonth = 0m, totalOutboundMonth = 0m });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);

        var lastDate = await dbContext.DailyInventoryRecords.AsNoTracking()
            .Where(record => record.ImportId == dailyImportId.Value && record.ProductionQuantity > 0)
            .MaxAsync(record => (DateOnly?)record.Date, cancellationToken);

        var lastProduction = 0m;
        var lastOutbound = 0m;

        if (lastDate.HasValue)
        {
            var daily = await dbContext.DailyInventoryRecords.AsNoTracking()
                .Where(record => record.ImportId == dailyImportId.Value && record.Date == lastDate.Value)
                .GroupBy(_ => 1)
                .Select(grouped => new
                {
                    production = grouped.Sum(record => record.ProductionQuantity),
                    outbound = grouped.Sum(record => record.OutboundQuantity)
                })
                .SingleAsync(cancellationToken);
            lastProduction = daily.production;
            lastOutbound = daily.outbound;
        }

        var monthTotals = await dbContext.DailyInventoryRecords.AsNoTracking()
            .Where(record => record.ImportId == dailyImportId.Value && record.Date >= firstOfMonth)
            .GroupBy(_ => 1)
            .Select(grouped => new
            {
                production = grouped.Sum(record => record.ProductionQuantity),
                outbound = grouped.Sum(record => record.OutboundQuantity)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            lastDailyDate = lastDate?.ToString("yyyy-MM-dd"),
            lastProduction,
            lastOutbound,
            operationalBalance = lastProduction - lastOutbound,
            totalProductionMonth = monthTotals?.production ?? 0,
            totalOutboundMonth = monthTotals?.outbound ?? 0
        });
    }

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var dailyImportId = await CurrentDailyImportIdAsync(cancellationToken);
        if (!dailyImportId.HasValue)
            return Ok(new { page, pageSize, total = 0, items = Array.Empty<object>() });

        var query = dbContext.DailyInventoryRecords.AsNoTracking()
            .Where(record => record.ImportId == dailyImportId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(record =>
                record.Product!.Name.ToUpper().Contains(term) ||
                record.Product.ErpCode.ToUpper().Contains(term) ||
                record.Product.OperationalCode.ToUpper().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(dateFrom) && DateOnly.TryParse(dateFrom, out var fromDate))
            query = query.Where(record => record.Date >= fromDate);

        if (!string.IsNullOrWhiteSpace(dateTo) && DateOnly.TryParse(dateTo, out var toDate))
            query = query.Where(record => record.Date <= toDate);

        var total = await query.CountAsync(cancellationToken);

        var projected = query.Select(record => new
        {
            record.Id,
            record.ProductId,
            record.Product!.ErpCode,
            record.Product.OperationalCode,
            productName = record.Product.Name,
            record.Product.GroupCode,
            record.Product.Type,
            record.Date,
            record.ProductionQuantity,
            record.OutboundQuantity,
            record.AdjustmentQuantity,
            record.ClosingQuantity,
            record.FirstShiftProductionQuantity,
            record.SecondShiftProductionQuantity,
            record.ThirdShiftProductionQuantity
        });

        projected = sort switch
        {
            "date_desc" => projected.OrderByDescending(item => item.Date).ThenBy(item => item.productName),
            "production_desc" => projected.OrderByDescending(item => item.ProductionQuantity),
            "production_asc" => projected.OrderBy(item => item.ProductionQuantity),
            _ => projected.OrderByDescending(item => item.Date).ThenBy(item => item.productName)
        };

        var items = await projected.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("dates")]
    public async Task<ActionResult> Dates(CancellationToken cancellationToken)
    {
        var dailyImportId = await CurrentDailyImportIdAsync(cancellationToken);
        if (!dailyImportId.HasValue)
            return Ok(new { dates = Array.Empty<string>() });

        var dates = await dbContext.DailyInventoryRecords.AsNoTracking()
            .Where(record => record.ImportId == dailyImportId.Value)
            .Select(record => record.Date)
            .Distinct()
            .OrderByDescending(date => date)
            .Take(365)
            .Select(date => date.ToString("yyyy-MM-dd"))
            .ToListAsync(cancellationToken);

        return Ok(new { dates });
    }

    [HttpGet("filters")]
    public async Task<ActionResult> Filters(CancellationToken cancellationToken)
    {
        var dailyImportId = await CurrentDailyImportIdAsync(cancellationToken);
        if (!dailyImportId.HasValue)
            return Ok(new { types = Array.Empty<string>(), groups = Array.Empty<string>() });

        var types = await dbContext.DailyInventoryRecords.AsNoTracking()
            .Where(record => record.ImportId == dailyImportId.Value && record.Product!.Type != string.Empty)
            .Select(record => record.Product!.Type)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);

        var groups = await dbContext.DailyInventoryRecords.AsNoTracking()
            .Where(record => record.ImportId == dailyImportId.Value && record.Product!.GroupCode != string.Empty)
            .Select(record => record.Product!.GroupCode)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);

        return Ok(new { types, groups });
    }

    private async Task<Guid?> CurrentDailyImportIdAsync(CancellationToken cancellationToken) =>
        await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == DailyInventoryImportCodes.DataSource)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
}
