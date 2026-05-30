namespace InovaSkill.Importer.Domain.Entities;

public sealed class CustomerSummaryMonthly
{
    public long Id { get; set; }
    public long SourceFileJobId { get; set; }
    public DateTime MonthStartDate { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ProductGroup { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int Orders { get; set; }
    public decimal Revenue { get; set; }
    public decimal Quantity { get; set; }
    public decimal Weight { get; set; }
    public DateTime ProcessedAt { get; set; }
}

