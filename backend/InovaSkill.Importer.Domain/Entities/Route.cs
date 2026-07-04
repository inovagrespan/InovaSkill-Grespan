namespace InovaSkill.Importer.Domain.Entities;

public sealed class Route
{
    public Guid Id { get; set; }
    public Guid ImportId { get; set; }
    public RouteImport? Import { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Weekday { get; set; } = string.Empty;
    public Guid VehicleTypeId { get; set; }
    public VehicleType? VehicleType { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<RouteEntry> Entries { get; set; } = [];
}
