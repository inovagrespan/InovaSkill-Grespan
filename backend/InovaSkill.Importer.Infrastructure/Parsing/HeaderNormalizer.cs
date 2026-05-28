namespace InovaSkill.Importer.Infrastructure.Parsing;

internal static class HeaderNormalizer
{
    public static Dictionary<string, string> Normalize(IDictionary<string, string> row)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in row)
        {
            normalized[key.Trim().ToLowerInvariant()] = value?.Trim() ?? string.Empty;
        }
        return normalized;
    }

    public static string Read(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
