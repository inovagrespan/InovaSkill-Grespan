namespace InovaSkill.Importer.Domain.ValueObjects;

public sealed record ColumnSchema(string Name, bool Required, ColumnDataType DataType);
