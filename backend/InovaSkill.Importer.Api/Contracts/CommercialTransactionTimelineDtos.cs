namespace InovaSkill.Importer.Api.Contracts;

public sealed record CommercialTransactionTimelinePointDto(
    DateTime PeriodStart,
    decimal TotalAmount,
    decimal TotalQuantity,
    decimal TotalWeightKg,
    int RecordCount);

public sealed record CommercialTransactionTimelineResponseDto(
    string Granularity,
    IReadOnlyList<CommercialTransactionTimelinePointDto> Items);
