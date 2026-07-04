using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class RouteImport
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public RouteImportStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int ErrorCount { get; set; }
    public string? FailureMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public ICollection<RouteImportError> Errors { get; set; } = [];
    public ICollection<JobExecution> JobExecutions { get; set; } = [];
    public ICollection<Route> Routes { get; set; } = [];
}
