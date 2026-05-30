namespace InovaSkill.Importer.Domain.ValueObjects;

public sealed class FileSchema
{
    public string ImportFileTypeCode { get; }
    public IReadOnlyList<ColumnSchema> Columns { get; }

    public FileSchema(string importFileTypeCode, IReadOnlyList<ColumnSchema> columns)
    {
        ImportFileTypeCode = importFileTypeCode;
        Columns = columns;
    }
}
