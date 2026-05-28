namespace InovaSkill.Importer.Domain.ValueObjects;

public sealed class ImportedRow
{
    public int RowNumber { get; }
    public IReadOnlyDictionary<string, string> Values { get; }

    public ImportedRow(int rowNumber, IReadOnlyDictionary<string, string> values)
    {
        RowNumber = rowNumber;
        Values = values;
    }

    public string Get(string column)
    {
        return Values.TryGetValue(column, out var value) ? value : string.Empty;
    }
}
