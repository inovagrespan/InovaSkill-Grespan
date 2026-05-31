namespace InovaSkill.Importer.Domain.ValueObjects;

public sealed class TableRow
{
    public int RowNumber { get; }
    public IReadOnlyDictionary<string, string> Values { get; }

    public TableRow(int rowNumber, IReadOnlyDictionary<string, string> values)
    {
        RowNumber = rowNumber;
        Values = values;
    }

    public string Get(string column)
    {
        return Values.TryGetValue(column, out var value) ? value : string.Empty;
    }
}
