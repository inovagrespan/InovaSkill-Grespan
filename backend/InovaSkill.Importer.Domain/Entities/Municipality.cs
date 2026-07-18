namespace InovaSkill.Importer.Domain.Entities;

public sealed class Municipality
{
    public Guid Id { get; set; }
    public string StateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? IbgeCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public MunicipalityCoordinate? Coordinate { get; set; }
}
