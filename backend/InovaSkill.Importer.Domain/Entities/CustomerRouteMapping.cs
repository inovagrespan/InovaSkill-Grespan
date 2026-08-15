namespace InovaSkill.Importer.Domain.Entities;

public sealed class CustomerRouteMapping
{
    public Guid Id { get; set; }
    public Guid ImportId { get; set; }
    public RouteImport? Import { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public int SourceRowNumber { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Weekday { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string NormalizedRouteName { get; set; } = string.Empty;
    public string MarketName { get; set; } = string.Empty;
    public string MunicipalityName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
