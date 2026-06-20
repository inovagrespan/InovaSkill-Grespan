namespace InovaSkill.Importer.Api.Contracts;

public sealed record CommercialTransactionCompanySummaryDto(
    string CompanyName,
    int DocumentCount,
    string? SingleDocumentNumber,
    decimal TotalAmount,
    decimal TotalQuantity,
    decimal TotalWeightKg,
    decimal CurrentPeriodAmount,
    decimal PreviousPeriodAmount,
    decimal? GrowthPercent);

public sealed record CommercialTransactionSummaryResponseDto(
    int Page,
    int PageSize,
    int TotalItems,
    string Granularity,
    DateTime CurrentPeriodStart,
    DateTime PreviousPeriodStart,
    decimal CurrentPeriodTotalAmount,
    decimal PreviousPeriodTotalAmount,
    decimal? TotalGrowthPercent,
    int TotalRecords,
    decimal TotalAmount,
    decimal TotalQuantity,
    decimal TotalWeightKg,
    int TotalCompanies,
    IReadOnlyList<CommercialTransactionCompanySummaryDto> Items);

public sealed record CommercialInvoiceSummaryDto(
    string DocumentNumber,
    DateTime TransactionDate,
    string CustomerCode,
    string CustomerName,
    string City,
    string TransactionType,
    decimal TotalAmount,
    decimal TotalQuantity,
    decimal TotalWeightKg,
    int TotalItems);

public sealed record CommercialInvoiceSummaryResponseDto(
    int Page,
    int PageSize,
    int TotalItems,
    decimal TotalAmount,
    decimal TotalQuantity,
    decimal TotalWeightKg,
    IReadOnlyList<CommercialInvoiceSummaryDto> Items);

public sealed record CommercialInvoiceDetailsDto(
    string DocumentNumber,
    DateTime TransactionDate,
    string CustomerCode,
    string CustomerName,
    string City,
    string TransactionType,
    decimal TotalAmount,
    decimal TotalQuantity,
    decimal TotalWeightKg,
    int TotalItems,
    IReadOnlyList<CommercialTransactionDto> Items);

public sealed record CommercialInvoiceAnalyticsSummaryDto(
    int TotalInvoices,
    decimal TotalAmount,
    decimal TotalWeightKg,
    int TotalCustomers,
    int TotalItems,
    decimal TotalQuantity);

public sealed record CommercialInvoiceAnalyticsTrendPointDto(
    DateTime PeriodStart,
    int InvoiceCount,
    decimal TotalAmount,
    decimal TotalWeightKg);

public sealed record CommercialInvoiceAnalyticsRankingItemDto(
    string CustomerCode,
    string CustomerName,
    decimal TotalAmount,
    int InvoiceCount,
    int TotalItems,
    decimal TotalWeightKg);

public sealed record CommercialInvoiceAnalyticsResponseDto(
    string Granularity,
    CommercialInvoiceAnalyticsSummaryDto Summary,
    IReadOnlyList<CommercialInvoiceAnalyticsTrendPointDto> Trend,
    IReadOnlyList<CommercialInvoiceAnalyticsRankingItemDto> Ranking);
