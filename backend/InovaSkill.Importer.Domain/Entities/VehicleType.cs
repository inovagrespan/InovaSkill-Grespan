namespace InovaSkill.Importer.Domain.Entities;

public sealed class VehicleType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal CapacityKg { get; set; }
    public ICollection<Route> Routes { get; set; } = [];
}
