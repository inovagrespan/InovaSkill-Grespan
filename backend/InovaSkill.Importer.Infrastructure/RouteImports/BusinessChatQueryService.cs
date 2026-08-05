using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class BusinessChatQueryService(ImportDbContext dbContext) : IBusinessChatQueryService
{
    private const int ConsumptionCurrentPeriodDays = 30;
    private const int ConsumptionAveragePeriodDays = 90;
    private const int ConsumptionTimelineMonths = 12;
    private const int PercentScale = 100;
    private const int PercentDecimalPlaces = 1;
    private const int WeightDecimalPlaces = 3;
    private const int CurrencyDecimalPlaces = 2;
    private const int RecentCustomerMovementsLimit = 10;
    private const string SortCommittedDesc = "committed_desc";
    private const string SortCommittedPercentDesc = "committed_percent_desc";
    private const string SortDateDesc = "date_desc";
    private const string SortProductionDesc = "production_desc";
    private const string SortProductionAsc = "production_asc";
    private const string StatusAvailable = "AVAILABLE";
    private const string StatusStockout = "STOCKOUT";

    public async Task<IReadOnlyList<BusinessChatCustomerDto>> SearchCustomersAsync(
        string searchTerm,
        int limit,
        CancellationToken cancellationToken)
    {
        var currentImportId = await CurrentImportIdAsync(CustomerImportCodes.DataSource, cancellationToken);
        if (!currentImportId.HasValue)
        {
            return [];
        }

        var term = searchTerm.Trim().ToUpperInvariant();
        var normalizedMunicipalityTerm = MunicipalityNameNormalizer.Normalize(searchTerm);

        return await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == currentImportId.Value)
            .Where(snapshot =>
                snapshot.Customer!.ExternalCode.ToUpper().Contains(term) ||
                snapshot.LegalName.ToUpper().Contains(term) ||
                snapshot.TradeName.ToUpper().Contains(term) ||
                snapshot.Municipality!.NormalizedName.Contains(normalizedMunicipalityTerm))
            .OrderBy(snapshot => snapshot.Customer!.ExternalCode)
            .ThenBy(snapshot => snapshot.Customer!.BranchCode)
            .Take(limit)
            .Select(snapshot => new BusinessChatCustomerDto(
                snapshot.CustomerId,
                snapshot.Customer!.ExternalCode,
                snapshot.Customer.BranchCode,
                snapshot.LegalName,
                snapshot.TradeName,
                snapshot.Municipality!.Name,
                snapshot.Municipality.StateCode,
                snapshot.CustomerType))
            .ToListAsync(cancellationToken);
    }

    public async Task<BusinessChatCustomerConsumptionDto?> GetCustomerConsumptionSummaryAsync(
        Guid customerId,
        DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        var reference = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var currentStart = reference.AddDays(-(ConsumptionCurrentPeriodDays - 1));
        var previousStart = currentStart.AddDays(-ConsumptionCurrentPeriodDays);
        var averageStart = reference.AddDays(-(ConsumptionAveragePeriodDays - 1));
        var timelineStart = new DateOnly(reference.Year, reference.Month, 1)
            .AddMonths(-(ConsumptionTimelineMonths - 1));

        var customer = await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(snapshot =>
                snapshot.CustomerId == customerId &&
                snapshot.Import!.DataSource!.CurrentImportId == snapshot.ImportId)
            .Select(snapshot => new BusinessChatCustomerDto(
                snapshot.CustomerId,
                snapshot.Customer!.ExternalCode,
                snapshot.Customer.BranchCode,
                snapshot.LegalName,
                snapshot.TradeName,
                snapshot.Municipality!.Name,
                snapshot.Municipality.StateCode,
                snapshot.CustomerType))
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var sales = dbContext.FiscalDocumentItems.AsNoTracking()
            .Where(item =>
                item.FiscalDocument!.CustomerId == customerId &&
                item.FiscalDocument.MovementCategory == FiscalMovementCategory.Sale);

        var current = await sales
            .Where(item =>
                item.FiscalDocument!.IssueDate >= currentStart &&
                item.FiscalDocument.IssueDate <= reference)
            .SumAsync(item => item.GrossWeightKg, cancellationToken);
        var previous = await sales
            .Where(item =>
                item.FiscalDocument!.IssueDate >= previousStart &&
                item.FiscalDocument.IssueDate < currentStart)
            .SumAsync(item => item.GrossWeightKg, cancellationToken);
        var ninety = await sales
            .Where(item =>
                item.FiscalDocument!.IssueDate >= averageStart &&
                item.FiscalDocument.IssueDate <= reference)
            .SumAsync(item => item.GrossWeightKg, cancellationToken);
        var lastPurchase = await dbContext.FiscalDocuments.AsNoTracking()
            .Where(document =>
                document.CustomerId == customerId &&
                document.MovementCategory == FiscalMovementCategory.Sale)
            .MaxAsync(document => (DateOnly?)document.IssueDate, cancellationToken);
        var saleDocumentsLast30Days = await dbContext.FiscalDocuments.AsNoTracking()
            .CountAsync(document =>
                document.CustomerId == customerId &&
                document.MovementCategory == FiscalMovementCategory.Sale &&
                document.IssueDate >= currentStart &&
                document.IssueDate <= reference,
                cancellationToken);

        var monthlyFacts = await dbContext.FiscalDocuments.AsNoTracking()
            .Where(document =>
                document.CustomerId == customerId &&
                document.IssueDate >= timelineStart &&
                document.IssueDate <= reference)
            .Select(document => new
            {
                document.IssueDate.Year,
                document.IssueDate.Month,
                document.MovementCategory,
                GrossWeightKg = document.Items.Sum(item => item.GrossWeightKg),
                CalculatedAmount = document.Items.Sum(item =>
                    item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : 0)
            })
            .GroupBy(item => new { item.Year, item.Month, item.MovementCategory })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.MovementCategory,
                DocumentCount = group.Count(),
                GrossWeightKg = group.Sum(item => item.GrossWeightKg),
                CalculatedAmount = group.Sum(item => item.CalculatedAmount)
            })
            .ToListAsync(cancellationToken);

        var monthlyTimeline = Enumerable.Range(0, ConsumptionTimelineMonths)
            .Select(offset =>
            {
                var month = timelineStart.AddMonths(offset);
                var salesMonth = monthlyFacts.SingleOrDefault(item =>
                    item.Year == month.Year &&
                    item.Month == month.Month &&
                    item.MovementCategory == FiscalMovementCategory.Sale);
                var returnMonth = monthlyFacts.SingleOrDefault(item =>
                    item.Year == month.Year &&
                    item.Month == month.Month &&
                    item.MovementCategory == FiscalMovementCategory.Return);
                var bonusMonth = monthlyFacts.SingleOrDefault(item =>
                    item.Year == month.Year &&
                    item.Month == month.Month &&
                    item.MovementCategory == FiscalMovementCategory.Bonus);
                var salesWeight = salesMonth?.GrossWeightKg ?? 0;
                var salesDocuments = salesMonth?.DocumentCount ?? 0;
                return new BusinessChatCustomerMonthlyConsumptionDto(
                    month.ToString("yyyy-MM"),
                    salesWeight,
                    salesDocuments,
                    salesDocuments == 0 ? 0 : Math.Round(salesWeight / salesDocuments, WeightDecimalPlaces),
                    salesMonth?.CalculatedAmount ?? 0,
                    returnMonth?.GrossWeightKg ?? 0,
                    bonusMonth?.GrossWeightKg ?? 0);
            })
            .ToArray();

        var salesWeight12Months = monthlyTimeline.Sum(item => item.SalesWeightKg);
        var salesDocuments12Months = monthlyTimeline.Sum(item => item.SalesDocumentCount);
        var calculatedSalesAmount12Months = monthlyTimeline.Sum(item => item.CalculatedSalesAmount);
        var metrics = new BusinessChatCustomerConsumptionMetricsDto(
            current,
            previous,
            previous == 0 ? null : Math.Round((current - previous) / previous * PercentScale, PercentDecimalPlaces),
            previous == 0 && current > 0 ? "NEW_ACTIVITY" : "COMPARABLE",
            Math.Round(ninety / (ConsumptionAveragePeriodDays / ConsumptionCurrentPeriodDays), WeightDecimalPlaces),
            Math.Round(salesWeight12Months / ConsumptionTimelineMonths, WeightDecimalPlaces),
            saleDocumentsLast30Days,
            salesDocuments12Months == 0
                ? 0
                : Math.Round(salesWeight12Months / salesDocuments12Months, WeightDecimalPlaces),
            Math.Round(calculatedSalesAmount12Months / ConsumptionTimelineMonths, CurrencyDecimalPlaces),
            monthlyTimeline.Sum(item => item.ReturnWeightKg),
            monthlyTimeline.Sum(item => item.BonusWeightKg),
            lastPurchase);

        var movements = await dbContext.FiscalDocuments.AsNoTracking()
            .Where(document => document.CustomerId == customerId)
            .OrderByDescending(document => document.IssueDate)
            .ThenByDescending(document => document.DocumentNumber)
            .Take(RecentCustomerMovementsLimit)
            .Select(document => new BusinessChatCustomerRecentMovementDto(
                document.Id,
                document.IssueDate,
                document.DocumentNumber,
                document.Series,
                document.DocumentType,
                document.MovementType,
                document.MovementCategory.ToString(),
                document.OperationCode,
                document.OperationDescription,
                document.OriginalDocumentNumber,
                document.Items.Count,
                document.Items.Sum(item => item.GrossWeightKg),
                document.Items.Sum(item =>
                    item.SourceTotalValue ?? (item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : 0))))
            .ToListAsync(cancellationToken);

        return new BusinessChatCustomerConsumptionDto(customer, metrics, monthlyTimeline, movements);
    }

    public async Task<IReadOnlyList<BusinessChatFiscalDocumentDto>> ListRecentFiscalDocumentsAsync(
        BusinessChatFiscalDocumentQuery fiscalQuery,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FiscalDocuments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(fiscalQuery.SearchTerm))
        {
            var term = fiscalQuery.SearchTerm.Trim().ToUpperInvariant();
            query = query.Where(document =>
                document.DocumentNumber.ToUpper().Contains(term) ||
                document.CustomerNameAtIssue.ToUpper().Contains(term) ||
                document.CustomerCodeAtIssue.ToUpper().Contains(term) ||
                document.CityNameAtIssue.ToUpper().Contains(term));
        }

        if (Enum.TryParse<FiscalMovementCategory>(fiscalQuery.OperationCategory, true, out var operationCategory))
        {
            query = query.Where(document => document.MovementCategory == operationCategory);
        }

        if (fiscalQuery.DateFrom.HasValue)
        {
            query = query.Where(document => document.IssueDate >= fiscalQuery.DateFrom.Value);
        }

        if (fiscalQuery.DateTo.HasValue)
        {
            query = query.Where(document => document.IssueDate <= fiscalQuery.DateTo.Value);
        }

        if (fiscalQuery.CustomerId.HasValue)
        {
            query = query.Where(document => document.CustomerId == fiscalQuery.CustomerId.Value);
        }

        return await query
            .OrderByDescending(document => document.IssueDate)
            .ThenByDescending(document => document.DocumentNumber)
            .Take(fiscalQuery.Limit)
            .Select(document => new BusinessChatFiscalDocumentDto(
                document.Id,
                document.IssueDate,
                document.DocumentNumber,
                document.Series,
                document.DocumentType,
                document.MovementType,
                document.CustomerId,
                document.CustomerNameAtIssue,
                document.CustomerCodeAtIssue,
                document.BranchCodeAtIssue,
                document.CityNameAtIssue,
                document.StateCodeAtIssue,
                document.MovementCategory.ToString(),
                document.OperationCode,
                document.OperationDescription,
                document.OriginalDocumentNumber,
                document.Items.Count,
                document.Items.Sum(item => item.GrossWeightKg),
                document.Items
                    .OrderBy(item => item.ItemNumber)
                    .Select(item => new BusinessChatFiscalDocumentPricingItemDto(
                        item.ItemNumber,
                        item.ProductCode,
                        item.ProductDescription,
                        item.ProductGroupCode,
                        item.ProductGroupDescription,
                        item.Quantity,
                        item.GrossWeightKg,
                        item.UnitValue,
                        item.SourceTotalValue,
                        item.SourceTotalValue ?? (item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : null),
                        item.Expenses,
                        item.Ipi,
                        item.Icms,
                        item.Iss,
                        item.CfopCode,
                        item.CfopDescription,
                        item.TesCode,
                        item.TesDescription,
                        item.OrderNumber,
                        item.WarehouseCode))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<BusinessChatFiscalReturnRateDto> GetFiscalReturnRateAsync(
        int periodDays,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var referenceDate = dateTo ?? await dbContext.FiscalDocuments.AsNoTracking()
            .MaxAsync(document => (DateOnly?)document.IssueDate, cancellationToken);
        if (!referenceDate.HasValue)
        {
            return new BusinessChatFiscalReturnRateDto(periodDays, null, null, 0, 0, 0);
        }

        var dateFrom = referenceDate.Value.AddDays(-(periodDays - 1));
        var weights = await dbContext.FiscalDocumentItems.AsNoTracking()
            .Where(item =>
                item.FiscalDocument!.IssueDate >= dateFrom &&
                item.FiscalDocument.IssueDate <= referenceDate.Value &&
                (item.FiscalDocument.MovementCategory == FiscalMovementCategory.Sale ||
                    item.FiscalDocument.MovementCategory == FiscalMovementCategory.Return))
            .GroupBy(item => item.FiscalDocument!.MovementCategory)
            .Select(group => new
            {
                Category = group.Key,
                GrossWeightKg = group.Sum(item => item.GrossWeightKg)
            })
            .ToListAsync(cancellationToken);

        var salesWeightKg = weights.SingleOrDefault(item => item.Category == FiscalMovementCategory.Sale)?.GrossWeightKg ?? 0;
        var returnWeightKg = weights.SingleOrDefault(item => item.Category == FiscalMovementCategory.Return)?.GrossWeightKg ?? 0;
        var returnRatePercent = salesWeightKg <= 0
            ? 0
            : Math.Round(returnWeightKg / salesWeightKg * PercentScale, PercentDecimalPlaces);

        return new BusinessChatFiscalReturnRateDto(
            periodDays,
            dateFrom,
            referenceDate.Value,
            salesWeightKg,
            returnWeightKg,
            returnRatePercent);
    }

    public async Task<IReadOnlyList<BusinessChatProductDto>> SearchProductsAsync(
        string searchTerm,
        int limit,
        CancellationToken cancellationToken)
    {
        var inventoryImportId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        var term = searchTerm.Trim().ToUpperInvariant();

        var products = await dbContext.Products.AsNoTracking()
            .Where(product =>
                product.Name.ToUpper().Contains(term) ||
                product.Description.ToUpper().Contains(term) ||
                product.ExternalCode.ToUpper().Contains(term) ||
                product.ErpCode.ToUpper().Contains(term) ||
                product.OperationalCode.ToUpper().Contains(term) ||
                product.Gtin.ToUpper().Contains(term))
            .OrderBy(product => product.Name)
            .ThenBy(product => product.ErpCode)
            .Take(limit)
            .Select(product => new
            {
                product.Id,
                product.ExternalCode,
                product.Description,
                product.ErpCode,
                product.OperationalCode,
                product.Name,
                product.Type,
                product.Unit,
                product.GroupCode,
                product.NetWeightKg,
                product.GrossWeightKg,
                product.Gtin,
                Inventory = inventoryImportId.HasValue
                    ? dbContext.InventorySnapshots
                        .Where(snapshot =>
                            snapshot.ImportId == inventoryImportId.Value &&
                            snapshot.ProductId == product.Id)
                        .GroupBy(snapshot => snapshot.ProductId)
                        .Select(group => new BusinessChatProductInventoryDto(
                            group.Sum(snapshot => snapshot.OnHandQuantity),
                            group.Sum(snapshot => snapshot.CommittedQuantity),
                            group.Sum(snapshot => snapshot.AvailableQuantity),
                            group.Sum(snapshot => snapshot.StockValue),
                            group.Sum(snapshot => snapshot.CommittedValue)))
                        .SingleOrDefault()
                    : null
            })
            .ToListAsync(cancellationToken);

        return products
            .Select(product => new BusinessChatProductDto(
                product.Id,
                product.ExternalCode,
                product.Description,
                product.ErpCode,
                product.OperationalCode,
                product.Name,
                product.Type,
                product.Unit,
                product.GroupCode,
                product.NetWeightKg,
                product.GrossWeightKg,
                product.Gtin,
                product.Inventory))
            .ToList();
    }

    public async Task<BusinessChatProductDetailsDto?> GetProductDetailsAsync(
        Guid productId,
        int inventoryHistoryLimit,
        int productionHistoryLimit,
        int fiscalItemsLimit,
        CancellationToken cancellationToken)
    {
        var inventoryImportId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        var dailyImportId = await CurrentImportIdAsync(DailyInventoryImportCodes.DataSource, cancellationToken);
        var product = await dbContext.Products.AsNoTracking()
            .Where(item => item.Id == productId)
            .Select(item => new BusinessChatProductCoreDto(
                item.Id,
                item.ExternalCode,
                item.Description,
                item.ErpCode,
                item.OperationalCode,
                item.Name,
                item.Type,
                item.Unit,
                item.GroupCode,
                item.NetWeightKg,
                item.GrossWeightKg,
                item.Gtin,
                item.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return null;
        }

        var latestInventory = inventoryImportId.HasValue
            ? await ProjectInventoryPositions(dbContext.InventorySnapshots.AsNoTracking()
                    .Where(snapshot =>
                        snapshot.ImportId == inventoryImportId.Value &&
                        snapshot.ProductId == productId))
                .OrderBy(position => position.BranchCode)
                .ThenBy(position => position.WarehouseCode)
                .ToListAsync(cancellationToken)
            : [];

        var inventoryHistory = await dbContext.InventorySnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ProductId == productId)
            .OrderByDescending(snapshot => snapshot.Import!.CreatedAt)
            .Take(inventoryHistoryLimit)
            .Select(snapshot => new BusinessChatInventoryHistoryDto(
                snapshot.ImportId,
                snapshot.Import!.CreatedAt,
                snapshot.BranchCode,
                snapshot.WarehouseCode,
                snapshot.OnHandQuantity,
                snapshot.CommittedQuantity,
                snapshot.AvailableQuantity,
                snapshot.StockValue,
                snapshot.CommittedValue))
            .ToListAsync(cancellationToken);

        var productionHistory = dailyImportId.HasValue
            ? await ProjectProductionRecords(dbContext.DailyInventoryRecords.AsNoTracking()
                    .Where(record =>
                        record.ImportId == dailyImportId.Value &&
                        record.ProductId == productId))
                .OrderByDescending(record => record.Date)
                .Take(productionHistoryLimit)
                .ToListAsync(cancellationToken)
            : [];

        var fiscalItems = await dbContext.FiscalDocumentItems.AsNoTracking()
            .Where(item => item.ProductId == productId)
            .OrderByDescending(item => item.FiscalDocument!.IssueDate)
            .ThenByDescending(item => item.FiscalDocument!.DocumentNumber)
            .Take(fiscalItemsLimit)
            .Select(item => new BusinessChatProductFiscalItemDto(
                item.Id,
                item.FiscalDocumentId,
                item.FiscalDocument!.IssueDate,
                item.FiscalDocument.DocumentNumber,
                item.FiscalDocument.Series,
                item.FiscalDocument.CustomerNameAtIssue,
                item.FiscalDocument.MovementCategory.ToString(),
                item.Quantity,
                item.GrossWeightKg,
                item.UnitValue,
                item.SourceTotalValue,
                item.SourceTotalValue ?? (item.UnitValue.HasValue ? item.Quantity * item.UnitValue.Value : null),
                item.Expenses,
                item.Ipi,
                item.Icms,
                item.Iss,
                item.CfopCode,
                item.TesCode,
                item.OrderNumber,
                item.WarehouseCode))
            .ToListAsync(cancellationToken);

        return new BusinessChatProductDetailsDto(
            product,
            latestInventory,
            inventoryHistory,
            productionHistory,
            fiscalItems);
    }

    public async Task<BusinessChatInventorySummaryDto> GetInventorySummaryAsync(CancellationToken cancellationToken)
    {
        var inventoryImportId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        var dailyImportId = await CurrentImportIdAsync(DailyInventoryImportCodes.DataSource, cancellationToken);
        var stockoutProducts = 0;
        var stockoutWarehousePositions = 0;
        decimal totalOnHandQuantity = 0;
        decimal totalCommittedQuantity = 0;
        decimal totalAvailableQuantity = 0;
        decimal totalStockValue = 0;
        decimal totalCommittedValue = 0;
        decimal committedPercent = 0;
        DateOnly? lastDailyDate = null;
        decimal lastProduction = 0;
        decimal lastOutbound = 0;

        if (inventoryImportId.HasValue)
        {
            stockoutProducts = await dbContext.InventorySnapshots.AsNoTracking()
                .Where(snapshot => snapshot.ImportId == inventoryImportId.Value)
                .GroupBy(snapshot => snapshot.ProductId)
                .CountAsync(group => group.Sum(snapshot => snapshot.AvailableQuantity) <= 0, cancellationToken);
            stockoutWarehousePositions = await dbContext.InventorySnapshots.AsNoTracking()
                .Where(snapshot =>
                    snapshot.ImportId == inventoryImportId.Value &&
                    snapshot.AvailableQuantity <= 0)
                .CountAsync(cancellationToken);
            var totals = await dbContext.InventorySnapshots.AsNoTracking()
                .Where(snapshot => snapshot.ImportId == inventoryImportId.Value)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Committed = group.Sum(snapshot => snapshot.CommittedQuantity),
                    OnHand = group.Sum(snapshot => snapshot.OnHandQuantity),
                    Available = group.Sum(snapshot => snapshot.AvailableQuantity),
                    StockValue = group.Sum(snapshot => snapshot.StockValue),
                    CommittedValue = group.Sum(snapshot => snapshot.CommittedValue)
                })
                .SingleOrDefaultAsync(cancellationToken);
            committedPercent = totals is null || totals.OnHand == 0
                ? 0
                : Math.Round(totals.Committed / totals.OnHand * PercentScale, 2);
            totalOnHandQuantity = totals?.OnHand ?? 0;
            totalCommittedQuantity = totals?.Committed ?? 0;
            totalAvailableQuantity = totals?.Available ?? 0;
            totalStockValue = totals?.StockValue ?? 0;
            totalCommittedValue = totals?.CommittedValue ?? 0;
        }

        if (dailyImportId.HasValue)
        {
            lastDailyDate = await dbContext.DailyInventoryRecords.AsNoTracking()
                .Where(record => record.ImportId == dailyImportId.Value)
                .MaxAsync(record => (DateOnly?)record.Date, cancellationToken);
            if (lastDailyDate.HasValue)
            {
                var daily = await dbContext.DailyInventoryRecords.AsNoTracking()
                    .Where(record =>
                        record.ImportId == dailyImportId.Value &&
                        record.Date == lastDailyDate.Value)
                    .GroupBy(_ => 1)
                    .Select(group => new
                    {
                        Production = group.Sum(record => record.ProductionQuantity),
                        Outbound = group.Sum(record => record.OutboundQuantity)
                    })
                    .SingleOrDefaultAsync(cancellationToken);
                lastProduction = daily?.Production ?? 0;
                lastOutbound = daily?.Outbound ?? 0;
            }
        }

        return new BusinessChatInventorySummaryDto(
            stockoutProducts,
            stockoutWarehousePositions,
            totalOnHandQuantity,
            totalCommittedQuantity,
            totalAvailableQuantity,
            totalStockValue,
            totalCommittedValue,
            committedPercent,
            lastDailyDate,
            lastProduction,
            lastOutbound,
            lastProduction - lastOutbound);
    }

    public async Task<IReadOnlyList<BusinessChatInventoryPositionDto>> ListInventoryPositionsAsync(
        BusinessChatInventoryPositionQuery inventoryQuery,
        CancellationToken cancellationToken)
    {
        var importId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        if (!importId.HasValue)
        {
            return [];
        }

        var query = dbContext.InventorySnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == importId.Value);

        if (inventoryQuery.ProductId.HasValue)
        {
            query = query.Where(snapshot => snapshot.ProductId == inventoryQuery.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(inventoryQuery.Warehouse))
        {
            query = query.Where(snapshot => snapshot.WarehouseCode == inventoryQuery.Warehouse.Trim());
        }

        if (!string.IsNullOrWhiteSpace(inventoryQuery.SearchTerm))
        {
            var term = inventoryQuery.SearchTerm.Trim().ToUpperInvariant();
            query = query.Where(snapshot =>
                snapshot.Product!.Name.ToUpper().Contains(term) ||
                snapshot.Product.Description.ToUpper().Contains(term) ||
                snapshot.Product.ExternalCode.ToUpper().Contains(term) ||
                snapshot.Product.ErpCode.ToUpper().Contains(term) ||
                snapshot.Product.OperationalCode.ToUpper().Contains(term) ||
                snapshot.Product.Gtin.ToUpper().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(inventoryQuery.Status))
        {
            var normalizedStatus = inventoryQuery.Status.Trim().ToUpperInvariant();
            query = normalizedStatus switch
            {
                StatusAvailable => query.Where(snapshot => snapshot.AvailableQuantity > 0),
                StatusStockout => query.Where(snapshot => snapshot.AvailableQuantity <= 0),
                _ => query
            };
        }

        var projected = ProjectInventoryPositions(query);
        projected = inventoryQuery.Sort switch
        {
            SortCommittedDesc => projected.OrderByDescending(item => item.CommittedQuantity),
            SortCommittedPercentDesc => projected.OrderByDescending(item => item.CommittedPercent),
            _ => projected.OrderBy(item => item.AvailableQuantity).ThenBy(item => item.ProductName)
        };

        return await projected.Take(inventoryQuery.Limit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessChatStockoutProductDto>> ListStockoutProductsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var importId = await CurrentImportIdAsync(InventoryCurrentImportCodes.DataSource, cancellationToken);
        if (!importId.HasValue)
        {
            return [];
        }

        var stockouts = await dbContext.InventorySnapshots.AsNoTracking()
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
                CommittedValue = group.Sum(snapshot => snapshot.CommittedValue),
                AffectedWarehousePositions = group.Count(snapshot => snapshot.AvailableQuantity <= 0),
                WarehousePositions = group.Count()
            })
            .Where(item => item.AvailableQuantity <= 0)
            .OrderBy(item => item.AvailableQuantity)
            .ThenByDescending(item => item.CommittedQuantity)
            .ThenBy(item => item.ProductName)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return stockouts
            .Select(item => new BusinessChatStockoutProductDto(
                item.ProductId,
                item.ErpCode,
                item.OperationalCode,
                item.ProductName,
                item.Type,
                item.Unit,
                item.GroupCode,
                item.OnHandQuantity,
                item.CommittedQuantity,
                item.AvailableQuantity,
                item.StockValue,
                item.CommittedValue,
                item.AffectedWarehousePositions,
                item.WarehousePositions))
            .ToList();
    }

    public async Task<BusinessChatProductionSummaryDto> GetProductionSummaryAsync(CancellationToken cancellationToken)
    {
        var dailyImportId = await CurrentImportIdAsync(DailyInventoryImportCodes.DataSource, cancellationToken);
        if (!dailyImportId.HasValue)
        {
            return new BusinessChatProductionSummaryDto(null, 0, 0, 0, 0, null, null, null, 0, 0, 0, 0);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDate = await dbContext.DailyInventoryRecords.AsNoTracking()
            .Where(record =>
                record.ImportId == dailyImportId.Value &&
                record.ProductionQuantity > 0)
            .MaxAsync(record => (DateOnly?)record.Date, cancellationToken);

        decimal lastProduction = 0;
        decimal lastOutbound = 0;
        decimal lastAdjustment = 0;
        decimal lastClosing = 0;
        decimal? lastFirstShiftProduction = null;
        decimal? lastSecondShiftProduction = null;
        decimal? lastThirdShiftProduction = null;
        if (lastDate.HasValue)
        {
            var daily = await dbContext.DailyInventoryRecords.AsNoTracking()
                .Where(record =>
                    record.ImportId == dailyImportId.Value &&
                    record.Date == lastDate.Value)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Production = group.Sum(record => record.ProductionQuantity),
                    Outbound = group.Sum(record => record.OutboundQuantity),
                    Adjustment = group.Sum(record => record.AdjustmentQuantity),
                    Closing = group.Sum(record => record.ClosingQuantity),
                    FirstShift = group.Sum(record => record.FirstShiftProductionQuantity),
                    SecondShift = group.Sum(record => record.SecondShiftProductionQuantity),
                    ThirdShift = group.Sum(record => record.ThirdShiftProductionQuantity)
                })
                .SingleOrDefaultAsync(cancellationToken);
            lastProduction = daily?.Production ?? 0;
            lastOutbound = daily?.Outbound ?? 0;
            lastAdjustment = daily?.Adjustment ?? 0;
            lastClosing = daily?.Closing ?? 0;
            lastFirstShiftProduction = daily?.FirstShift;
            lastSecondShiftProduction = daily?.SecondShift;
            lastThirdShiftProduction = daily?.ThirdShift;
        }

        var monthTotals = await dbContext.DailyInventoryRecords.AsNoTracking()
            .Where(record =>
                record.ImportId == dailyImportId.Value &&
                record.Date >= firstOfMonth)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Production = group.Sum(record => record.ProductionQuantity),
                Outbound = group.Sum(record => record.OutboundQuantity),
                Adjustment = group.Sum(record => record.AdjustmentQuantity)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new BusinessChatProductionSummaryDto(
            lastDate,
            lastProduction,
            lastOutbound,
            lastAdjustment,
            lastClosing,
            lastFirstShiftProduction,
            lastSecondShiftProduction,
            lastThirdShiftProduction,
            lastProduction - lastOutbound,
            monthTotals?.Production ?? 0,
            monthTotals?.Outbound ?? 0,
            monthTotals?.Adjustment ?? 0);
    }

    public async Task<IReadOnlyList<BusinessChatProductionRecordDto>> ListProductionRecordsAsync(
        BusinessChatProductionRecordQuery productionQuery,
        CancellationToken cancellationToken)
    {
        var dailyImportId = await CurrentImportIdAsync(DailyInventoryImportCodes.DataSource, cancellationToken);
        if (!dailyImportId.HasValue)
        {
            return [];
        }

        var query = dbContext.DailyInventoryRecords.AsNoTracking()
            .Where(record => record.ImportId == dailyImportId.Value);

        if (productionQuery.ProductId.HasValue)
        {
            query = query.Where(record => record.ProductId == productionQuery.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(productionQuery.SearchTerm))
        {
            var term = productionQuery.SearchTerm.Trim().ToUpperInvariant();
            query = query.Where(record =>
                record.Product!.Name.ToUpper().Contains(term) ||
                record.Product.Description.ToUpper().Contains(term) ||
                record.Product.ExternalCode.ToUpper().Contains(term) ||
                record.Product.ErpCode.ToUpper().Contains(term) ||
                record.Product.OperationalCode.ToUpper().Contains(term) ||
                record.Product.Gtin.ToUpper().Contains(term));
        }

        if (productionQuery.DateFrom.HasValue)
        {
            query = query.Where(record => record.Date >= productionQuery.DateFrom.Value);
        }

        if (productionQuery.DateTo.HasValue)
        {
            query = query.Where(record => record.Date <= productionQuery.DateTo.Value);
        }

        var projected = ProjectProductionRecords(query);
        projected = productionQuery.Sort switch
        {
            SortProductionDesc => projected.OrderByDescending(item => item.ProductionQuantity),
            SortProductionAsc => projected.OrderBy(item => item.ProductionQuantity),
            SortDateDesc => projected.OrderByDescending(item => item.Date).ThenBy(item => item.ProductName),
            _ => projected.OrderByDescending(item => item.Date).ThenBy(item => item.ProductName)
        };

        return await projected.Take(productionQuery.Limit).ToListAsync(cancellationToken);
    }

    private async Task<Guid?> CurrentImportIdAsync(string sourceCode, CancellationToken cancellationToken) =>
        await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == sourceCode)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);

    private static IQueryable<BusinessChatInventoryPositionDto> ProjectInventoryPositions(
        IQueryable<InventorySnapshot> query) =>
        query.Select(snapshot => new BusinessChatInventoryPositionDto(
            snapshot.Id,
            snapshot.ProductId,
            snapshot.Product!.ErpCode,
            snapshot.Product.OperationalCode,
            snapshot.Product.Name,
            snapshot.Product.Type,
            snapshot.Product.Unit,
            snapshot.Product.GroupCode,
            snapshot.BranchCode,
            snapshot.WarehouseCode,
            snapshot.OnHandQuantity,
            snapshot.CommittedQuantity,
            snapshot.AvailableQuantity,
            snapshot.StockValue,
            snapshot.CommittedValue,
            snapshot.OnHandQuantity == 0
                ? null
                : Math.Round(snapshot.CommittedQuantity / snapshot.OnHandQuantity * PercentScale, 2)));

    private static IQueryable<BusinessChatProductionRecordDto> ProjectProductionRecords(
        IQueryable<DailyInventoryRecord> query) =>
        query.Select(record => new BusinessChatProductionRecordDto(
            record.Id,
            record.ProductId,
            record.Product!.ExternalCode,
            record.Product.Description,
            record.Product.ErpCode,
            record.Product.OperationalCode,
            record.Product.Name,
            record.Product.Gtin,
            record.Product.GroupCode,
            record.Product.Type,
            record.Date,
            record.ProductionQuantity,
            record.OutboundQuantity,
            record.AdjustmentQuantity,
            record.ClosingQuantity,
            record.FirstShiftProductionQuantity,
            record.SecondShiftProductionQuantity,
            record.ThirdShiftProductionQuantity));
}
