namespace InovaSkill.Importer.Domain.Entities;

public sealed class FiscalDocumentItem
{
    public Guid Id { get; set; }
    public Guid FiscalDocumentId { get; set; }
    public FiscalDocument? FiscalDocument { get; set; }
    public string ItemNumber { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public string ProductGroupCode { get; set; } = string.Empty;
    public string ProductGroupDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal GrossWeightKg { get; set; }
    public decimal? UnitValue { get; set; }
    public decimal? SourceTotalValue { get; set; }
    public decimal? Expenses { get; set; }
    public decimal? Ipi { get; set; }
    public decimal? Icms { get; set; }
    public decimal? Iss { get; set; }
    public string? CfopCode { get; set; }
    public string? CfopDescription { get; set; }
    public string? TesCode { get; set; }
    public string? TesDescription { get; set; }
    public string? OrderNumber { get; set; }
    public string? WarehouseCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
