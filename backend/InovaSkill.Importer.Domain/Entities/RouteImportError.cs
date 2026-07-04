using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class RouteImportError
{
    public Guid Id { get; set; }
    public Guid ImportId { get; set; }
    public RouteImport? Import { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string Field { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ImportErrorStatus Status { get; set; }
    public string? CorrectedValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
