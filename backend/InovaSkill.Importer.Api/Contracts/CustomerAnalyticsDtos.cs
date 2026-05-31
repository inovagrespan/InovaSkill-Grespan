namespace InovaSkill.Importer.Api.Contracts;

public sealed record CustomerAnalyticsSummaryDto(
    int ActiveCustomers,
    decimal TotalRevenue,
    int TotalOrders,
    decimal AverageTicket,
    decimal AverageRevenuePerCustomer,
    int NewCustomers,
    int InactiveCustomers,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime PreviousPeriodStart,
    DateTime PreviousPeriodEnd);

public sealed record CustomerRankingItemDto(
    string CustomerCode,
    string CustomerName,
    decimal Revenue,
    decimal Quantity,
    decimal Weight,
    int Orders,
    decimal AverageTicket,
    decimal? VariationPercent);

public sealed record CustomerRankingResponseDto(
    int Page,
    int PageSize,
    int TotalItems,
    IReadOnlyList<CustomerRankingItemDto> Items);

public sealed record CustomerNewCustomersMonthlyPointDto(
    DateTime MonthStart,
    int NewCustomers);

public sealed record CustomerNewCustomersMonthlyResponseDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int TotalNewCustomers,
    int ActiveMonths,
    IReadOnlyList<CustomerNewCustomersMonthlyPointDto> Points);

public sealed record CustomerDetailsDto(
    string CustomerCode,
    string CustomerName,
    decimal Revenue,
    decimal Quantity,
    decimal Weight,
    int Orders,
    decimal AverageTicket,
    DateTime? LastPurchaseDate,
    decimal? AverageDaysBetweenPurchases);

public sealed record CustomerEvolutionPointDto(
    DateTime PeriodStart,
    decimal Revenue,
    decimal Quantity,
    decimal Weight,
    int Orders);

public sealed record CustomerProductItemDto(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal Revenue,
    decimal SharePercent);

public sealed record CustomerSummaryResponseDto(
    string CustomerCode,
    string CustomerName,
    string City,
    string LinkedCompany,
    DateTime? LastPurchaseDate,
    string Status,
    decimal TotalRevenue,
    decimal? AverageTicket,
    decimal? AverageRevenueMonthly,
    decimal? AverageRevenueWeekly,
    decimal TotalQuantity,
    decimal TotalWeight,
    int TotalOrders,
    decimal? AverageDaysBetweenPurchases);

public sealed record CustomerTimelineResponseDto(
    string Granularity,
    string Metric,
    IReadOnlyList<CustomerTimelinePointDto> Points);

public sealed record CustomerTimelinePointDto(
    DateTime PeriodStart,
    decimal Value,
    decimal Revenue,
    decimal Quantity,
    decimal Weight,
    int Orders);

public sealed record CustomerComparisonItemDto(
    string Label,
    decimal CurrentValue,
    decimal PreviousValue,
    decimal? VariationPercent);

public sealed record CustomerComparisonResponseDto(
    IReadOnlyList<CustomerComparisonItemDto> Items);

public sealed record CustomerPurchaseHistoryItemDto(
    DateTime Date,
    string Document,
    string Product,
    decimal Quantity,
    decimal UnitPrice,
    decimal Total,
    decimal Weight,
    string OperationType);

public sealed record CustomerPurchaseHistoryResponseDto(
    int Page,
    int PageSize,
    int TotalItems,
    IReadOnlyList<CustomerPurchaseHistoryItemDto> Items);

public sealed record CustomerInsightsResponseDto(
    decimal? AveragePurchaseFrequencyDays,
    DateTime? EstimatedNextPurchaseDate,
    decimal? PredictedRevenue,
    decimal? PredictedQuantity,
    string ConsumptionTrend,
    string RiskLevel,
    int DaysWithoutPurchase,
    decimal? RiskScore,
    string? FrequencyReason,
    string? NextPurchaseReason,
    string? RevenuePredictionReason,
    string? QuantityPredictionReason,
    string? RiskReason,
    int MonthlyHistoryPeriods);
