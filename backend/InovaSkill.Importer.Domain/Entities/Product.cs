namespace InovaSkill.Importer.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string ExternalCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ErpCode { get; set; } = string.Empty;
    public string OperationalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public decimal? NetWeightKg { get; set; }
    public decimal? GrossWeightKg { get; set; }
    public string Gtin { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
