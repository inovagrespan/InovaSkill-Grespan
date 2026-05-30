namespace InovaSkill.Importer.Domain.Entities;

public sealed class CommercialTransaction
{
    public long Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ProductGroup { get; set; } = string.Empty;
    public decimal GrossWeightKg { get; set; }
    public long SourceFileJobId { get; set; }
}

