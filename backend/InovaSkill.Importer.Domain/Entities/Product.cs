namespace InovaSkill.Importer.Domain.Entities;

public sealed class Product
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
    public long SourceFileJobId { get; set; }
}
