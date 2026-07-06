namespace InovaSkill.Importer.Domain.Entities;

public sealed class CustomerSnapshot
{
    public Guid Id { get; set; }
    public Guid ImportId { get; set; }
    public RouteImport? Import { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "UNKNOWN";
    public string LegalName { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public int SourceRowNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
