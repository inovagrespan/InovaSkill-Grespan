namespace InovaSkill.Importer.Api.Contracts;

public sealed record FinanceDashboardSummaryDto(
    decimal TotalRevenue,
    int TotalOrders,
    decimal TotalQuantity,
    decimal AverageTicket);

public sealed record FinanceDashboardItemDto(
    string Customer,
    DateTime Date,
    decimal Revenue,
    int Orders,
    decimal Quantity);

public sealed record FinanceRevenueTrendPointDto(
    string Period,
    string Label,
    decimal Revenue);

public sealed record FinanceCustomerRevenuePointDto(
    string Customer,
    decimal Revenue);

public sealed record FinanceDashboardResponseDto(
    IReadOnlyList<string> Customers,
    FinanceDashboardSummaryDto Summary,
    IReadOnlyList<FinanceRevenueTrendPointDto> RevenueTrend,
    IReadOnlyList<FinanceCustomerRevenuePointDto> CustomerRanking,
    IReadOnlyList<FinanceDashboardItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
