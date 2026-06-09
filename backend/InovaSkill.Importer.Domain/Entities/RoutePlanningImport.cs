namespace InovaSkill.Importer.Domain.Entities;

public sealed class RoutePlanningImport
{
    public long Id { get; set; }
    public long SourceFileJobId { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public ICollection<RoutePlan> Routes { get; set; } = [];
    public ICollection<TruckCapacityProfile> TruckCapacities { get; set; } = [];
}
