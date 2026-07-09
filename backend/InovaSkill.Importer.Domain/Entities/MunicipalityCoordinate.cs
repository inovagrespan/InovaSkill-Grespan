namespace InovaSkill.Importer.Domain.Entities;

public sealed class MunicipalityCoordinate
{
    public Guid Id { get; set; }
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = MunicipalityCoordinateStatuses.Resolved;
    public string? FailureReason { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class MunicipalityCoordinateStatuses
{
    public const string Resolved = "RESOLVED";
    public const string Failed = "FAILED";
}
