using System.Globalization;

namespace InovaSkill.Importer.Infrastructure.Processing.Buffers;

internal static class UtcDateTimeParser
{
    private static readonly string[] AcceptedDateTimeFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
        "yyyyMMdd",
        "yyyyMMdd HH:mm:ss",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd/MM/yyyy HH:mm",
        "d/M/yyyy H:mm",
        "dd/MM/yyyy HH:mm:ss",
        "d/M/yyyy H:mm:ss",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "MM/dd/yyyy HH:mm",
        "M/d/yyyy H:mm",
        "MM/dd/yyyy HH:mm:ss",
        "M/d/yyyy H:mm:ss"
    ];

    private static readonly CultureInfo[] FallbackCultures =
    [
        CultureInfo.GetCultureInfo("pt-BR"),
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("en-US")
    ];

    public static DateTime ParseRequired(string value)
    {
        if (TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"DateTime inválido: '{value}'.");
    }

    public static DateTime ParseOrDefaultUtcNow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.UtcNow;
        }

        if (TryParse(value, out var parsed))
        {
            return parsed;
        }

        return DateTime.UtcNow;
    }

    public static bool TryParse(string? value, out DateTime parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (DateTime.TryParseExact(
                trimmed,
                AcceptedDateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var exact))
        {
            parsed = EnsureUtc(exact);
            return true;
        }

        foreach (var culture in FallbackCultures)
        {
            if (DateTime.TryParse(
                    trimmed,
                    culture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var fallback))
            {
                parsed = EnsureUtc(fallback);
                return true;
            }
        }

        return false;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
