using InovaSkill.Importer.Application.Abstractions;
using System.Globalization;
using System.Text.Json;

namespace InovaSkill.Importer.Infrastructure.Processing.TransformRules;

public sealed class BrazilianDateRule : ITransformRule
{
    public string Code => "BrazilianDate";

    public object? Apply(object? value, string? parametersJson)
    {
        if (value is null)
        {
            return null;
        }

        var raw = value.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            using var doc = JsonDocument.Parse(parametersJson);
            if (doc.RootElement.TryGetProperty("formats", out var formatsElement) && formatsElement.ValueKind == JsonValueKind.Array)
            {
                formats = formatsElement.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();
            }
        }

        if (DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Data invalida: '{raw}'.");
    }
}
