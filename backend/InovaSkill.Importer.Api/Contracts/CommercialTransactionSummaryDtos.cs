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
