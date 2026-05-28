namespace InovaSkill.Importer.Domain.Entities;

public sealed class Order
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime OrderedAt { get; set; }
    public long SourceFileJobId { get; set; }
}
