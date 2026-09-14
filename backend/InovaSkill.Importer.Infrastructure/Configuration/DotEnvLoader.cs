using System.Text;

namespace InovaSkill.Importer.Infrastructure.Configuration;

/// <summary>
/// Carrega variáveis do arquivo .env para facilitar a execução local.
/// Variáveis já definidas no ambiente sempre têm precedência.
/// </summary>
public static class DotEnvLoader
{
    public static void Load(string? startingDirectory = null)
    {
        var filePath = FindFile(startingDirectory);
        if (filePath is null)
        {
            return;
        }

        var values = Parse(File.ReadLines(filePath));
        foreach (var (key, value) in values)
        {
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            if (!IsValidKey(key))
            {
                continue;
            }

            var value = ParseValue(line[(separatorIndex + 1)..].Trim());
            values[key] = value;
        }

        return values;
    }

    private static string? FindFile(string? startingDirectory)
    {
        var directory = new DirectoryInfo(startingDirectory ?? Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string ParseValue(string value)
    {
        if (value.Length >= 2 && (value[0] == '\'' || value[0] == '"'))
        {
            var closingQuoteIndex = value.LastIndexOf(value[0]);
            if (closingQuoteIndex > 0)
            {
                var quoted = value[1..closingQuoteIndex];
                return value[0] == '"' ? DecodeDoubleQuotedValue(quoted) : quoted;
            }
        }

        var commentIndex = value.IndexOf(" #", StringComparison.Ordinal);
        return commentIndex >= 0 ? value[..commentIndex].TrimEnd() : value;
    }

    private static string DecodeDoubleQuotedValue(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\' || index == value.Length - 1)
            {
                builder.Append(value[index]);
                continue;
            }

            var escaped = value[++index];
            builder.Append(escaped switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => escaped,
            });
        }

        return builder.ToString();
    }

    private static bool IsValidKey(string key) =>
        key.Length > 0 &&
        (char.IsLetter(key[0]) || key[0] == '_') &&
        key.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
}
