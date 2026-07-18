using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class RouteCustomerAssignment
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public Route? Route { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public RouteCustomerAssignmentSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
