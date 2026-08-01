using System.Text.Json;
using System.Text.RegularExpressions;

namespace InovaSkill.Importer.Api.Assistant;

public static partial class ExternalResearchQuerySanitizer
{
    private const int MinimumSafeQueryLength = 12;
    private const int MinimumSensitiveTextLength = 4;

    public static string? Sanitize(string query, IReadOnlyList<string> internalPayloads)
    {
        var sanitized = SensitiveNumberRegex().Replace(query, " ");
        sanitized = MonetaryValueRegex().Replace(sanitized, " ");
        sanitized = InternalCodeRegex().Replace(sanitized, " ");
        sanitized = PotentialProperNameRegex().Replace(sanitized, " ");

        foreach (var sensitiveText in ReadSensitiveTexts(internalPayloads))
        {
            sanitized = Regex.Replace(
                sanitized,
                Regex.Escape(sensitiveText),
                " ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        sanitized = WhitespaceRegex().Replace(sanitized, " ").Trim(' ', '.', ',', ';', ':', '-', '|');
        return sanitized.Length >= MinimumSafeQueryLength ? sanitized : null;
    }

    private static IEnumerable<string> ReadSensitiveTexts(IReadOnlyList<string> payloads)
    {
        foreach (var payload in payloads)
        {
            JsonDocument? document = null;
            try { document = JsonDocument.Parse(payload); }
            catch (JsonException) { }
            if (document is null) continue;
            using (document)
            {
                foreach (var value in ReadStrings(document.RootElement))
                {
                    if (value.Length >= MinimumSensitiveTextLength) yield return value;
                }
            }
        }
    }

    private static IEnumerable<string> ReadStrings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(value)) yield return value;
            yield break;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            foreach (var value in ReadStrings(property.Value)) yield return value;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            foreach (var value in ReadStrings(item)) yield return value;
        }
    }

    [GeneratedRegex(@"\b(?:\d[.\-\s/]*){6,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveNumberRegex();

    [GeneratedRegex(@"(?:R\$\s*)?\d+[,.]\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonetaryValueRegex();

    [GeneratedRegex(@"\b(?=[A-Z0-9._/-]*[A-Z])(?=[A-Z0-9._/-]*\d)[A-Z0-9._/-]{4,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex InternalCodeRegex();

    [GeneratedRegex(@"\b(?:[A-ZÀ-ÖØ-Þ][a-zà-öø-ÿ]{2,}\s+){1,}[A-ZÀ-ÖØ-Þ][a-zà-öø-ÿ]{2,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex PotentialProperNameRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
