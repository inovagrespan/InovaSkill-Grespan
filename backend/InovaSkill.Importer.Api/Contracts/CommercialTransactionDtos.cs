namespace InovaSkill.Importer.Api.Contracts;

public sealed record CommercialTransactionDto(
    long Id,
    string DocumentNumber,
    DateTime TransactionDate,
    string CustomerCode,
    string CustomerName,
    string SupplierCode,
    string SupplierName,
    string RouteName,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string TransactionType,
    string City,
    string ProductGroup,
    decimal GrossWeightKg,
    long SourceFileJobId);

