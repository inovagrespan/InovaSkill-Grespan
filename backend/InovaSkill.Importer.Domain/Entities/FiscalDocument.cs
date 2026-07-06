using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class FiscalDocument
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public string CustomerCodeAtIssue { get; set; } = string.Empty;
    public string BranchCodeAtIssue { get; set; } = string.Empty;
    public string CustomerNameAtIssue { get; set; } = string.Empty;
    public string CityNameAtIssue { get; set; } = string.Empty;
    public string StateCodeAtIssue { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;
    public string OperationDescription { get; set; } = string.Empty;
    public FiscalMovementCategory MovementCategory { get; set; }
    public string? OriginalDocumentNumber { get; set; }
    public Guid FirstSeenImportId { get; set; }
    public RouteImport? FirstSeenImport { get; set; }
    public Guid LastSeenImportId { get; set; }
    public RouteImport? LastSeenImport { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<FiscalDocumentItem> Items { get; set; } = [];
}
