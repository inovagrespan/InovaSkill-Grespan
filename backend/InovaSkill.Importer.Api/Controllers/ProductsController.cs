using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ImportDbContext dbContext) : ControllerBase
{
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 100;
    private const int RecentFiscalItemsLimit = 20;

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] string? group = null,
        [FromQuery] string? stockStatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var inventoryImportId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        var query = ApplyProductFilters(dbContext.Products.AsNoTracking(), search, type, group);
        query = ApplyStockStatusFilter(query, inventoryImportId, stockStatus);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(product => product.Name).ThenBy(product => product.ErpCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(product => new
            {
                product.Id,
                product.ErpCode,
                product.OperationalCode,
                product.Name,
                product.Type,
                product.Unit,
                product.GroupCode,
                product.NetWeightKg,
                product.GrossWeightKg,
                product.Gtin,
                inventory = inventoryImportId.HasValue
                    ? dbContext.InventorySnapshots.Where(snapshot =>
                            snapshot.ImportId == inventoryImportId.Value && snapshot.ProductId == product.Id)
                        .GroupBy(snapshot => snapshot.ProductId)
                        .Select(grouped => new
                        {
                            onHandQuantity = grouped.Sum(snapshot => snapshot.OnHandQuantity),
                            committedQuantity = grouped.Sum(snapshot => snapshot.CommittedQuantity),
                            availableQuantity = grouped.Sum(snapshot => snapshot.AvailableQuantity),
                            stockValue = grouped.Sum(snapshot => snapshot.StockValue)
                        })
                        .SingleOrDefault()
                    : null
            })
            .ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var inventoryImportId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        var dailyImportId = await CurrentImportIdAsync(DailyInventoryImportCodes.DataSource, cancellationToken);
        var product = await dbContext.Products.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.ErpCode,
                item.OperationalCode,
                item.Name,
                item.Type,
                item.Unit,
                item.GroupCode,
                item.NetWeightKg,
                item.GrossWeightKg,
                item.Gtin,
                item.CreatedAt,
                item.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) return NotFound();

        var latestInventory = inventoryImportId.HasValue
            ? await dbContext.InventorySnapshots.AsNoTracking()
                .Where(snapshot => snapshot.ImportId == inventoryImportId.Value && snapshot.ProductId == id)
                .OrderBy(snapshot => snapshot.BranchCode).ThenBy(snapshot => snapshot.WarehouseCode)
                .Select(snapshot => new
                {
                    snapshot.BranchCode,
                    snapshot.WarehouseCode,
                    snapshot.OnHandQuantity,
                    snapshot.CommittedQuantity,
                    snapshot.AvailableQuantity,
                    snapshot.StockValue,
                    snapshot.CommittedValue
                })
                .ToListAsync(cancellationToken)
            : [];
        var inventoryHistory = await dbContext.InventorySnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ProductId == id)
            .OrderByDescending(snapshot => snapshot.Import!.CreatedAt)
            .Take(30)
            .Select(snapshot => new
            {
                snapshot.ImportId,
                importCreatedAt = snapshot.Import!.CreatedAt,
                snapshot.BranchCode,
                snapshot.WarehouseCode,
                snapshot.OnHandQuantity,
                snapshot.CommittedQuantity,
                snapshot.AvailableQuantity,
                snapshot.StockValue
            })
            .ToListAsync(cancellationToken);
        var dailyHistory = dailyImportId.HasValue
            ? await dbContext.DailyInventoryRecords.AsNoTracking()
                .Where(record => record.ImportId == dailyImportId.Value && record.ProductId == id)
                .OrderByDescending(record => record.Date)
                .Take(90)
                .Select(record => new
                {
                    record.Date,
                    record.ProductionQuantity,
                    record.OutboundQuantity,
                    record.AdjustmentQuantity,
                    record.ClosingQuantity,
                    record.FirstShiftProductionQuantity,
                    record.SecondShiftProductionQuantity,
                    record.ThirdShiftProductionQuantity
                })
                .ToListAsync(cancellationToken)
            : [];
        var fiscalItems = await dbContext.FiscalDocumentItems.AsNoTracking()
            .Where(item => item.ProductId == id)
            .OrderByDescending(item => item.FiscalDocument!.IssueDate)
            .ThenByDescending(item => item.FiscalDocument!.DocumentNumber)
            .Take(RecentFiscalItemsLimit)
            .Select(item => new
            {
                item.Id,
                item.FiscalDocumentId,
                issueDate = item.FiscalDocument!.IssueDate,
                documentNumber = item.FiscalDocument.DocumentNumber,
                item.FiscalDocument.Series,
                customerName = item.FiscalDocument.CustomerNameAtIssue,
                operationCategory = item.FiscalDocument.MovementCategory.ToString(),
                item.Quantity,
                item.GrossWeightKg,
                item.UnitValue,
                calculatedAmount = item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : 0
            })
            .ToListAsync(cancellationToken);

        return Ok(new { product, latestInventory, inventoryHistory, dailyHistory, fiscalItems });
    }

    [HttpGet("filters")]
    public async Task<ActionResult> Filters(CancellationToken cancellationToken)
    {
        var types = await dbContext.Products.AsNoTracking()
            .Where(product => product.Type != string.Empty)
            .Select(product => product.Type).Distinct().OrderBy(value => value)
            .ToListAsync(cancellationToken);
        var groups = await dbContext.Products.AsNoTracking()
            .Where(product => product.GroupCode != string.Empty)
            .Select(product => product.GroupCode).Distinct().OrderBy(value => value)
            .ToListAsync(cancellationToken);
        return Ok(new { types, groups });
    }

    internal static IQueryable<InovaSkill.Importer.Domain.Entities.Product> ApplyProductFilters(
        IQueryable<InovaSkill.Importer.Domain.Entities.Product> query,
        string? search,
        string? type,
        string? group)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(product =>
                product.Name.ToUpper().Contains(term) ||
                product.ErpCode.ToUpper().Contains(term) ||
                product.OperationalCode.ToUpper().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(product => product.Type == type.Trim());
        if (!string.IsNullOrWhiteSpace(group))
            query = query.Where(product => product.GroupCode == group.Trim());
        return query;
    }

    private IQueryable<InovaSkill.Importer.Domain.Entities.Product> ApplyStockStatusFilter(
        IQueryable<InovaSkill.Importer.Domain.Entities.Product> query,
        Guid? inventoryImportId,
        string? stockStatus)
    {
        if (string.IsNullOrWhiteSpace(stockStatus)) return query;
        var status = stockStatus.Trim().ToUpperInvariant();
        if (!inventoryImportId.HasValue) return status == "NO_INFORMATION" ? query : query.Where(_ => false);
        return status switch
        {
            "AVAILABLE" => query.Where(product => dbContext.InventorySnapshots
                .Where(snapshot => snapshot.ImportId == inventoryImportId.Value && snapshot.ProductId == product.Id)
                .Sum(snapshot => (decimal?)snapshot.AvailableQuantity) > 0),
            "STOCKOUT" => query.Where(product => dbContext.InventorySnapshots
                .Any(snapshot => snapshot.ImportId == inventoryImportId.Value && snapshot.ProductId == product.Id) &&
                dbContext.InventorySnapshots
                    .Where(snapshot => snapshot.ImportId == inventoryImportId.Value && snapshot.ProductId == product.Id)
                    .Sum(snapshot => (decimal?)snapshot.AvailableQuantity) <= 0),
            "NO_INFORMATION" => query.Where(product => !dbContext.InventorySnapshots
                .Any(snapshot => snapshot.ImportId == inventoryImportId.Value && snapshot.ProductId == product.Id)),
            _ => query
        };
    }

    private async Task<Guid?> CurrentImportIdAsync(string sourceCode, CancellationToken cancellationToken) =>
        await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == sourceCode)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
}
