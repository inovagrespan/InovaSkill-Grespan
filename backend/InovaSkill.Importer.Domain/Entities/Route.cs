using InovaSkill.Importer.Domain.Enums;

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
    public decimal TotalWeightKg { get; set; }
    public decimal? TotalVolumeM3 { get; set; }
    public int? TotalPallets { get; set; }
    public decimal? WeightOccupancy { get; set; }
    public decimal? VolumeOccupancy { get; set; }
    public decimal? PalletOccupancy { get; set; }
    public decimal? OverallOccupancy { get; set; }
    public RouteOccupancyStatus OccupancyStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<RouteEntry> Entries { get; set; } = [];
    public ICollection<RouteCustomerAssignment> CustomerAssignments { get; set; } = [];
}
