namespace InovaSkill.Importer.Application.RouteImports;

public interface IRouteChatQueryService
{
    Task<IReadOnlyList<RouteChatSummaryDto>> SearchRoutesAsync(
        string searchTerm,
        int limit,
        CancellationToken cancellationToken);

    Task<RouteChatDetailsDto?> GetRouteDetailsAsync(
        Guid routeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RouteChatCriticalDto>> GetCriticalRoutesAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RouteChatSummaryDto>> ListRoutesByOccupancyAsync(
        RouteChatOccupancyQuery query,
        CancellationToken cancellationToken);

    Task<RouteChatCitiesDto?> GetRouteCitiesAsync(
        Guid routeId,
        int limit,
        CancellationToken cancellationToken);

    Task<RouteChatRouteCustomersDto?> GetRouteCustomersAsync(
        Guid routeId,
        int limit,
        CancellationToken cancellationToken);
}

public interface IBusinessChatQueryService
{
    Task<IReadOnlyList<BusinessChatCustomerDto>> SearchCustomersAsync(
        string searchTerm,
        int limit,
        CancellationToken cancellationToken);

    Task<BusinessChatCustomerConsumptionDto?> GetCustomerConsumptionSummaryAsync(
        Guid customerId,
        DateOnly? referenceDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BusinessChatFiscalDocumentDto>> ListRecentFiscalDocumentsAsync(
        BusinessChatFiscalDocumentQuery query,
        CancellationToken cancellationToken);

    Task<BusinessChatFiscalReturnRateDto> GetFiscalReturnRateAsync(
        int periodDays,
        DateOnly? dateTo,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BusinessChatProductDto>> SearchProductsAsync(
        string searchTerm,
        int limit,
        CancellationToken cancellationToken);

    Task<BusinessChatProductDetailsDto?> GetProductDetailsAsync(
        Guid productId,
        int inventoryHistoryLimit,
        int productionHistoryLimit,
        int fiscalItemsLimit,
        CancellationToken cancellationToken);

    Task<BusinessChatInventorySummaryDto> GetInventorySummaryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BusinessChatInventoryPositionDto>> ListInventoryPositionsAsync(
        BusinessChatInventoryPositionQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BusinessChatStockoutProductDto>> ListStockoutProductsAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<BusinessChatProductionSummaryDto> GetProductionSummaryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BusinessChatProductionRecordDto>> ListProductionRecordsAsync(
        BusinessChatProductionRecordQuery query,
        CancellationToken cancellationToken);
}

public sealed record RouteChatOccupancyQuery(
    string? OccupancyLevel,
    decimal? MinimumOccupancyPercentage,
    decimal? MaximumOccupancyPercentage,
    string SortDirection,
    int Limit);

public sealed record RouteChatSummaryDto(
    Guid Id,
    string Name,
    string Status,
    decimal? OccupancyPercentage);

public sealed record RouteChatDetailsDto(
    Guid Id,
    string Name,
    string Status,
    decimal? OccupancyPercentage,
    int CityCount,
    int DeliveryCount,
    int PotentialCustomerCount,
    DateTime UpdatedAt);

public sealed record RouteChatCriticalDto(
    Guid Id,
    string Name,
    string Status,
    decimal? OccupancyPercentage,
    string Reason);

public sealed record RouteChatCitiesDto(
    Guid RouteId,
    string RouteName,
    IReadOnlyList<RouteChatCityDto> Cities);

public sealed record RouteChatCityDto(string Name, string? State);

public sealed record RouteChatRouteCustomersDto(
    Guid RouteId,
    string RouteName,
    string RelationshipType,
    string RelationshipDescription,
    IReadOnlyList<RouteChatCustomerDto> Customers);

public sealed record RouteChatCustomerDto(
    Guid Id,
    string Code,
    string BranchCode,
    string Name,
    string TradeName,
    string MunicipalityName,
    string State,
    string CustomerType);

public sealed record BusinessChatCustomerDto(
    Guid Id,
    string Code,
    string BranchCode,
    string LegalName,
    string TradeName,
    string MunicipalityName,
    string State,
    string CustomerType);

public sealed record BusinessChatCustomerConsumptionDto(
    BusinessChatCustomerDto Customer,
    BusinessChatCustomerConsumptionMetricsDto Metrics,
    IReadOnlyList<BusinessChatCustomerMonthlyConsumptionDto> MonthlyTimeline,
    IReadOnlyList<BusinessChatCustomerRecentMovementDto> RecentMovements);

public sealed record BusinessChatCustomerConsumptionMetricsDto(
    decimal SalesWeightLast30Days,
    decimal SalesWeightPrevious30Days,
    decimal? VariationPercentage,
    string VariationStatus,
    decimal AverageMonthlySalesWeight90Days,
    decimal AverageMonthlySalesWeight12Months,
    int SaleDocumentsLast30Days,
    decimal AverageSalesWeightPerDocument12Months,
    decimal AverageMonthlyCalculatedSalesAmount12Months,
    decimal ReturnWeight12Months,
    decimal BonusWeight12Months,
    DateOnly? LastPurchaseDate);

public sealed record BusinessChatCustomerMonthlyConsumptionDto(
    string Month,
    decimal SalesWeightKg,
    int SalesDocumentCount,
    decimal AverageSalesWeightPerDocumentKg,
    decimal CalculatedSalesAmount,
    decimal ReturnWeightKg,
    decimal BonusWeightKg);

public sealed record BusinessChatCustomerRecentMovementDto(
    Guid Id,
    DateOnly IssueDate,
    string DocumentNumber,
    string Series,
    string OperationCategory,
    string OperationDescription,
    int ItemCount,
    decimal GrossWeightKg);

public sealed record BusinessChatFiscalDocumentQuery(
    string? SearchTerm,
    string? OperationCategory,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    Guid? CustomerId,
    int Limit);

public sealed record BusinessChatFiscalDocumentDto(
    Guid Id,
    DateOnly IssueDate,
    string DocumentNumber,
    string Series,
    Guid? CustomerId,
    string CustomerName,
    string CustomerCode,
    string BranchCode,
    string CityName,
    string State,
    string OperationCategory,
    string OperationDescription,
    int ItemCount,
    decimal GrossWeightKg);

public sealed record BusinessChatFiscalReturnRateDto(
    int PeriodDays,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    decimal SalesWeightKg,
    decimal ReturnWeightKg,
    decimal ReturnRatePercent);

public sealed record BusinessChatProductDto(
    Guid Id,
    string ErpCode,
    string OperationalCode,
    string Name,
    string Type,
    string Unit,
    string GroupCode,
    decimal? NetWeightKg,
    decimal? GrossWeightKg,
    BusinessChatProductInventoryDto? Inventory);

public sealed record BusinessChatProductInventoryDto(
    decimal OnHandQuantity,
    decimal CommittedQuantity,
    decimal AvailableQuantity,
    decimal StockValue);

public sealed record BusinessChatProductDetailsDto(
    BusinessChatProductCoreDto Product,
    IReadOnlyList<BusinessChatInventoryPositionDto> LatestInventory,
    IReadOnlyList<BusinessChatInventoryHistoryDto> InventoryHistory,
    IReadOnlyList<BusinessChatProductionRecordDto> ProductionHistory,
    IReadOnlyList<BusinessChatProductFiscalItemDto> RecentFiscalItems);

public sealed record BusinessChatProductCoreDto(
    Guid Id,
    string ErpCode,
    string OperationalCode,
    string Name,
    string Type,
    string Unit,
    string GroupCode,
    decimal? NetWeightKg,
    decimal? GrossWeightKg,
    DateTime UpdatedAt);

public sealed record BusinessChatInventoryPositionQuery(
    string? SearchTerm,
    Guid? ProductId,
    string? Warehouse,
    string? Status,
    string Sort,
    int Limit);

public sealed record BusinessChatInventoryPositionDto(
    Guid Id,
    Guid ProductId,
    string ErpCode,
    string OperationalCode,
    string ProductName,
    string Type,
    string Unit,
    string GroupCode,
    string BranchCode,
    string WarehouseCode,
    decimal OnHandQuantity,
    decimal CommittedQuantity,
    decimal AvailableQuantity,
    decimal StockValue,
    decimal? CommittedPercent);

public sealed record BusinessChatInventoryHistoryDto(
    Guid ImportId,
    DateTime ImportCreatedAt,
    string BranchCode,
    string WarehouseCode,
    decimal OnHandQuantity,
    decimal CommittedQuantity,
    decimal AvailableQuantity,
    decimal StockValue);

public sealed record BusinessChatProductFiscalItemDto(
    Guid Id,
    Guid FiscalDocumentId,
    DateOnly IssueDate,
    string DocumentNumber,
    string Series,
    string CustomerName,
    string OperationCategory,
    decimal Quantity,
    decimal GrossWeightKg,
    decimal? UnitValue,
    decimal CalculatedAmount);

public sealed record BusinessChatInventorySummaryDto(
    int StockoutProducts,
    int StockoutWarehousePositions,
    decimal CommittedPercent,
    DateOnly? LastDailyDate,
    decimal LastProduction,
    decimal LastOutbound,
    decimal OperationalBalance);

public sealed record BusinessChatStockoutProductDto(
    Guid ProductId,
    string ErpCode,
    string OperationalCode,
    string ProductName,
    string Type,
    string Unit,
    string GroupCode,
    decimal OnHandQuantity,
    decimal CommittedQuantity,
    decimal AvailableQuantity,
    decimal StockValue,
    int AffectedWarehousePositions,
    int WarehousePositions);

public sealed record BusinessChatProductionSummaryDto(
    DateOnly? LastDailyDate,
    decimal LastProduction,
    decimal LastOutbound,
    decimal OperationalBalance,
    decimal TotalProductionMonth,
    decimal TotalOutboundMonth);

public sealed record BusinessChatProductionRecordQuery(
    string? SearchTerm,
    Guid? ProductId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string Sort,
    int Limit);

public sealed record BusinessChatProductionRecordDto(
    Guid Id,
    Guid ProductId,
    string ErpCode,
    string OperationalCode,
    string ProductName,
    string GroupCode,
    string Type,
    DateOnly Date,
    decimal ProductionQuantity,
    decimal OutboundQuantity,
    decimal AdjustmentQuantity,
    decimal ClosingQuantity,
    decimal? FirstShiftProductionQuantity,
    decimal? SecondShiftProductionQuantity,
    decimal? ThirdShiftProductionQuantity);
