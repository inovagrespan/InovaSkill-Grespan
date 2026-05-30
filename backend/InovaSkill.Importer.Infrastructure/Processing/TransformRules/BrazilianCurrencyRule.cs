using InovaSkill.Importer.Application.Abstractions;
using System.Globalization;
using System.Text.Json;

namespace InovaSkill.Importer.Infrastructure.Processing.TransformRules;

public sealed class BrazilianCurrencyRule : ITransformRule
{
    public string Code => "BrazilianCurrency";

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

        var culture = "pt-BR";
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            using var doc = JsonDocument.Parse(parametersJson);
            if (doc.RootElement.TryGetProperty("culture", out var cultureElement) && cultureElement.ValueKind == JsonValueKind.String)
            {
                culture = cultureElement.GetString() ?? culture;
            }
        }

        var normalized = raw
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty)
            .Trim();

        var canonical = CanonicalizeNumber(normalized);
        if (decimal.TryParse(canonical, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Valor monetario invalido: '{raw}'.");
    }

    private static string CanonicalizeNumber(string value)
    {
        var lastComma = value.LastIndexOf(',');
        var lastDot = value.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot)
            {
                return value.Replace(".", string.Empty).Replace(",", ".");
            }

            return value.Replace(",", string.Empty);
        }

        if (lastComma >= 0)
        {
            return value.Replace(".", string.Empty).Replace(",", ".");
        }

        if (lastDot >= 0)
        {
            return value.Replace(",", string.Empty);
        }

        return value;
    }
}
