namespace InovaSkill.Importer.Domain.Entities;

public sealed class SalesSummaryWeekly
{
    public long Id { get; set; }
    public long SourceFileJobId { get; set; }
    public DateTime WeekStartDate { get; set; }
    public string City { get; set; } = string.Empty;
    public string ProductGroup { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalGrossWeightKg { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
