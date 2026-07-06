using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class DataSource
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ProcessorKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DataSourceImportMode ImportMode { get; set; }
    public long NextImportVersion { get; set; }
    public Guid? CurrentImportId { get; set; }
    public RouteImport? CurrentImport { get; set; }
    public Guid? LastSuccessfulImportId { get; set; }
    public RouteImport? LastSuccessfulImport { get; set; }
    public DateTime? StateUpdatedAt { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<RouteImport> Imports { get; set; } = [];
}
