namespace InovaSkill.Importer.Domain.Entities;

public sealed class RouteEntry
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public Route? Route { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public int Deliveries { get; set; }
    public decimal AveragePerDay { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
