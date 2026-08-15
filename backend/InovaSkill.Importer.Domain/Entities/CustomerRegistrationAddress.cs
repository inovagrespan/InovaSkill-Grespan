namespace InovaSkill.Importer.Domain.Entities;

public sealed class CustomerRegistrationAddress
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = CustomerRegistrationAddressStatuses.Resolved;
    public string? PostalCode { get; set; }
    public string? StateCode { get; set; }
    public string? City { get; set; }
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string? Neighborhood { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class CustomerRegistrationAddressStatuses
{
    public const string Resolved = "RESOLVED";
    public const string InvalidDocument = "INVALID_DOCUMENT";
    public const string NotFound = "NOT_FOUND";
    public const string Failed = "FAILED";
}
