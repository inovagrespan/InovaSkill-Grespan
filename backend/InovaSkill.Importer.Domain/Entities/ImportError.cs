namespace InovaSkill.Importer.Domain.Entities;

public sealed class ImportError
{
    public long Id { get; set; }
    public long FileJobId { get; set; }
    public int RowNumber { get; set; }
    public string Column { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RecordIdentifier { get; set; } = string.Empty;
}
