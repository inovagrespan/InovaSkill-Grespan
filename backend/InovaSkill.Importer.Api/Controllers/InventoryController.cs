using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(ImportDbContext dbContext) : ControllerBase
{
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 100;
    private const int DefaultStockoutPageSize = 20;

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] string? group = null,
        [FromQuery] string? warehouse = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var importId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        if (!importId.HasValue)
            return Ok(new { page, pageSize, total = 0, items = Array.Empty<object>() });

        var query = dbContext.InventorySnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == importId.Value);
        if (!string.IsNullOrWhiteSpace(warehouse))
            query = query.Where(snapshot => snapshot.WarehouseCode == warehouse.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(snapshot =>
                snapshot.Product!.Name.ToUpper().Contains(term) ||
                snapshot.Product.ErpCode.ToUpper().Contains(term) ||
                snapshot.Product.OperationalCode.ToUpper().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(snapshot => snapshot.Product!.Type == type.Trim());
        if (!string.IsNullOrWhiteSpace(group))
            query = query.Where(snapshot => snapshot.Product!.GroupCode == group.Trim());
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = normalizedStatus switch
            {
                "AVAILABLE" => query.Where(snapshot => snapshot.AvailableQuantity > 0),
                "STOCKOUT" => query.Where(snapshot => snapshot.AvailableQuantity <= 0),
                _ => query
            };
        }

        var total = await query.CountAsync(cancellationToken);
        var projected = query.Select(snapshot => new
        {
            snapshot.Id,
            snapshot.ProductId,
            snapshot.Product!.ErpCode,
            snapshot.Product.OperationalCode,
            productName = snapshot.Product.Name,
            snapshot.Product.Type,
            snapshot.Product.Unit,
            snapshot.Product.GroupCode,
            snapshot.BranchCode,
            snapshot.WarehouseCode,
            snapshot.OnHandQuantity,
            snapshot.CommittedQuantity,
            snapshot.AvailableQuantity,
            snapshot.StockValue,
            committedPercent = snapshot.OnHandQuantity == 0
                ? (decimal?)null
                : Math.Round(snapshot.CommittedQuantity / snapshot.OnHandQuantity * 100, 2)
        });
        projected = sort switch
        {
            "committed_desc" => projected.OrderByDescending(item => item.CommittedQuantity),
            "committed_percent_desc" => projected.OrderByDescending(item => item.committedPercent),
            _ => projected.OrderBy(item => item.AvailableQuantity).ThenBy(item => item.productName)
        };
        var items = await projected.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("summary")]
    public async Task<ActionResult> Summary(CancellationToken cancellationToken)
    {
        var inventoryImportId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        var dailyImportId = await CurrentImportIdAsync(DailyInventoryImportCodes.DataSource, cancellationToken);
        var stockouts = 0;
        var stockoutWarehousePositions = 0;
        decimal committedPercent = 0;
        DateOnly? lastDailyDate = null;
        decimal lastProduction = 0;
        decimal lastOutbound = 0;
        if (inventoryImportId.HasValue)
        {
            stockouts = await dbContext.InventorySnapshots.AsNoTracking()
                .Where(snapshot => snapshot.ImportId == inventoryImportId.Value)
                .GroupBy(snapshot => snapshot.ProductId)
                .CountAsync(grouped => grouped.Sum(snapshot => snapshot.AvailableQuantity) <= 0, cancellationToken);
            stockoutWarehousePositions = await dbContext.InventorySnapshots.AsNoTracking()
                .Where(snapshot => snapshot.ImportId == inventoryImportId.Value && snapshot.AvailableQuantity <= 0)
                .CountAsync(cancellationToken);
            var totals = await dbContext.InventorySnapshots.AsNoTracking()
                .Where(snapshot => snapshot.ImportId == inventoryImportId.Value)
                .GroupBy(_ => 1)
                .Select(grouped => new
                {
                    committed = grouped.Sum(snapshot => snapshot.CommittedQuantity),
                    onHand = grouped.Sum(snapshot => snapshot.OnHandQuantity)
                })
                .SingleOrDefaultAsync(cancellationToken);
            committedPercent = totals is null || totals.onHand == 0
                ? 0
                : Math.Round(totals.committed / totals.onHand * 100, 2);
        }
        if (dailyImportId.HasValue)
        {
            lastDailyDate = await dbContext.DailyInventoryRecords.AsNoTracking()
                .Where(record => record.ImportId == dailyImportId.Value)
                .MaxAsync(record => (DateOnly?)record.Date, cancellationToken);
            if (lastDailyDate.HasValue)
            {
                var daily = await dbContext.DailyInventoryRecords.AsNoTracking()
                    .Where(record => record.ImportId == dailyImportId.Value && record.Date == lastDailyDate.Value)
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
        }
        return Ok(new
        {
            stockouts,
            stockoutProducts = stockouts,
            stockoutWarehousePositions,
            committedPercent,
            lastDailyDate,
            lastProduction,
            lastOutbound,
            operationalBalance = lastProduction - lastOutbound
        });
    }

    [HttpGet("stockouts")]
    public async Task<ActionResult> Stockouts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultStockoutPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var importId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        if (!importId.HasValue)
            return Ok(new { page, pageSize, total = 0, items = Array.Empty<object>() });

        var grouped = dbContext.InventorySnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == importId.Value)
            .GroupBy(snapshot => new
            {
                snapshot.ProductId,
                snapshot.Product!.ErpCode,
                snapshot.Product.OperationalCode,
                ProductName = snapshot.Product.Name,
                snapshot.Product.Type,
                snapshot.Product.Unit,
                snapshot.Product.GroupCode
            })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ErpCode,
                group.Key.OperationalCode,
                group.Key.ProductName,
                group.Key.Type,
                group.Key.Unit,
                group.Key.GroupCode,
                OnHandQuantity = group.Sum(snapshot => snapshot.OnHandQuantity),
                CommittedQuantity = group.Sum(snapshot => snapshot.CommittedQuantity),
                AvailableQuantity = group.Sum(snapshot => snapshot.AvailableQuantity),
                StockValue = group.Sum(snapshot => snapshot.StockValue),
                AffectedWarehousePositions = group.Count(snapshot => snapshot.AvailableQuantity <= 0),
                WarehousePositions = group.Count()
            })
            .Where(item => item.AvailableQuantity <= 0);

        var total = await grouped.CountAsync(cancellationToken);
        var items = await grouped
            .OrderBy(item => item.AvailableQuantity)
            .ThenByDescending(item => item.CommittedQuantity)
            .ThenBy(item => item.ProductName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("filters")]
    public async Task<ActionResult> Filters(CancellationToken cancellationToken)
    {
        var importId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        var warehouses = importId.HasValue
            ? await dbContext.InventorySnapshots.AsNoTracking()
                .Where(snapshot => snapshot.ImportId == importId.Value && snapshot.WarehouseCode != string.Empty)
                .Select(snapshot => snapshot.WarehouseCode).Distinct().OrderBy(value => value)
                .ToListAsync(cancellationToken)
            : [];
        return Ok(new { warehouses });
    }

    private async Task<Guid?> CurrentImportIdAsync(string sourceCode, CancellationToken cancellationToken) =>
        await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == sourceCode)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
}
