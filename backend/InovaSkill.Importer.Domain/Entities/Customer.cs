namespace InovaSkill.Importer.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string ExternalCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastPurchaseAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<CustomerSnapshot> Snapshots { get; set; } = [];
    public ICollection<RouteCustomerAssignment> RouteAssignments { get; set; } = [];
    public CustomerRegistrationAddress? RegistrationAddress { get; set; }
}
