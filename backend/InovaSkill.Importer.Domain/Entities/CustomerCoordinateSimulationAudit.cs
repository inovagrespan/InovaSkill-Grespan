namespace InovaSkill.Importer.Domain.Entities;

public sealed class CustomerCoordinateSimulationAudit
{
    public Guid Id { get; set; }
    public Guid JobExecutionId { get; set; }
    public JobExecution? JobExecution { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? CustomerRegistrationAddressId { get; set; }
    public CustomerRegistrationAddress? CustomerRegistrationAddress { get; set; }
    public bool RegistrationAddressCreatedBySimulation { get; set; }
    public bool OriginalCoordinateExisted { get; set; }
    public string? OriginalNormalizedAddress { get; set; }
    public string? OriginalSource { get; set; }
    public string? OriginalStatus { get; set; }
    public string? OriginalPrecision { get; set; }
    public decimal? OriginalLatitude { get; set; }
    public decimal? OriginalLongitude { get; set; }
    public string? OriginalProviderPlaceId { get; set; }
    public string? OriginalDisplayName { get; set; }
    public string? OriginalFailureReason { get; set; }
    public DateTime? OriginalLastAttemptAt { get; set; }
    public DateTime? OriginalResolvedAt { get; set; }
    public DateTime? OriginalCreatedAt { get; set; }
    public DateTime? OriginalUpdatedAt { get; set; }
    public decimal BaseLatitude { get; set; }
    public decimal BaseLongitude { get; set; }
    public string BaseSource { get; set; } = string.Empty;
    public decimal SimulatedLatitude { get; set; }
    public decimal SimulatedLongitude { get; set; }
    public string SimulatedSource { get; set; } = string.Empty;
    public string ProviderPlaceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal DistanceMeters { get; set; }
    public long AppliedByUserId { get; set; }
    public AppUser? AppliedByUser { get; set; }
    public DateTime AppliedAt { get; set; }
    public Guid? RevertJobExecutionId { get; set; }
    public JobExecution? RevertJobExecution { get; set; }
    public long? RevertedByUserId { get; set; }
    public AppUser? RevertedByUser { get; set; }
    public DateTime? RevertedAt { get; set; }
}
