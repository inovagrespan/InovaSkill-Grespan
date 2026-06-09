namespace InovaSkill.Importer.Domain.Entities;

public sealed class RoutePlan
{
    public long Id { get; set; }
    public long RoutePlanningImportId { get; set; }
    public RoutePlanningImport? RoutePlanningImport { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public string WeekdayLabel { get; set; } = string.Empty;
    public int WeekdayOrder { get; set; }
    public int RouteOrder { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public decimal? VehicleCapacityKg { get; set; }
    public int? TotalDeliveries { get; set; }
    public decimal? TotalAverageLoadKg { get; set; }
    public decimal? OccupancyPercent { get; set; }
    public ICollection<RouteStop> Stops { get; set; } = [];
}
