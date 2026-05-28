using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.ValueObjects;

public sealed class FileSchema
{
    public FileType FileType { get; }
    public IReadOnlyList<ColumnSchema> Columns { get; }

    public FileSchema(FileType fileType, IReadOnlyList<ColumnSchema> columns)
    {
        FileType = fileType;
        Columns = columns;
    }
}
