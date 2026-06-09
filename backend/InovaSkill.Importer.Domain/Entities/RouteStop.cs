namespace InovaSkill.Importer.Domain.Entities;

public sealed class RouteStop
{
    public long Id { get; set; }
    public long RoutePlanId { get; set; }
    public RoutePlan? RoutePlan { get; set; }
    public int StopOrder { get; set; }
    public string DestinationName { get; set; } = string.Empty;
    public string DeliveriesRaw { get; set; } = string.Empty;
    public int? DeliveriesCount { get; set; }
    public decimal? AverageLoadKg { get; set; }
    public string Note { get; set; } = string.Empty;
}
