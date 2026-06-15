namespace InovaSkill.Importer.Domain.Entities;

public sealed class TruckCapacityProfile
{
    public long Id { get; set; }
    public long RoutePlanningImportId { get; set; }
    public RoutePlanningImport? RoutePlanningImport { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public decimal CapacityKg { get; set; }
}
