namespace InovaSkill.Importer.Domain.Entities;

public sealed class Customer
{
    public long Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SourceFileJobId { get; set; }
}
