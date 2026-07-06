namespace InovaSkill.Importer.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string ExternalCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
