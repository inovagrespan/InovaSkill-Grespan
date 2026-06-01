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

        var options = CurrencyRuleOptions.Default;
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            using var doc = JsonDocument.Parse(parametersJson);
            options = CurrencyRuleOptions.FromJson(doc.RootElement);
        }

        var normalized = raw
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty)
            .Trim();

        var canonical = CanonicalizeNumber(normalized, options.DecimalSeparator, options.ThousandSeparator);
        if (decimal.TryParse(canonical, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            if (!options.AllowNegative && parsed < 0)
            {
                throw new InvalidOperationException($"Valor monetario negativo nao permitido: '{raw}'.");
            }

            if (options.PositiveOnly && parsed <= 0)
            {
                throw new InvalidOperationException($"Valor monetario deve ser positivo: '{raw}'.");
            }

            if (options.MinValue.HasValue && parsed < options.MinValue.Value)
            {
                throw new InvalidOperationException($"Valor monetario abaixo do minimo permitido: '{raw}'.");
            }

            if (options.MaxValue.HasValue && parsed > options.MaxValue.Value)
            {
                throw new InvalidOperationException($"Valor monetario acima do maximo permitido: '{raw}'.");
            }

            return options.DecimalPlaces.HasValue ? decimal.Round(parsed, options.DecimalPlaces.Value) : parsed;
        }

        throw new InvalidOperationException($"Valor monetario invalido: '{raw}'.");
    }

    private static string CanonicalizeNumber(string value, string? decimalSeparator, string? thousandSeparator)
    {
        if (!string.IsNullOrEmpty(decimalSeparator))
        {
            var normalized = value;
            if (!string.IsNullOrEmpty(thousandSeparator))
            {
                normalized = normalized.Replace(thousandSeparator, string.Empty);
            }

            return normalized.Replace(decimalSeparator, ".");
        }

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

    private sealed record CurrencyRuleOptions(
        string? DecimalSeparator,
        string? ThousandSeparator,
        bool AllowNegative,
        bool PositiveOnly,
        decimal? MinValue,
        decimal? MaxValue,
        int? DecimalPlaces)
    {
        public static CurrencyRuleOptions Default { get; } = new(null, null, true, false, null, null, null);

        public static CurrencyRuleOptions FromJson(JsonElement root)
        {
            return new CurrencyRuleOptions(
                ReadString(root, "decimalSeparator"),
                ReadString(root, "thousandSeparator"),
                ReadBool(root, "allowNegative", true),
                ReadBool(root, "positiveOnly", false),
                ReadDecimal(root, "minValue"),
                ReadDecimal(root, "maxValue"),
                ReadInt(root, "decimalPlaces"));
        }

        private static string? ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
        }

        private static bool ReadBool(JsonElement root, string propertyName, bool defaultValue)
        {
            return root.TryGetProperty(propertyName, out var element) && element.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? element.GetBoolean()
                : defaultValue;
        }

        private static decimal? ReadDecimal(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var element))
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
            {
                return number;
            }

            return element.ValueKind == JsonValueKind.String &&
                decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null;
        }

        private static int? ReadInt(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var element) &&
                element.ValueKind == JsonValueKind.Number &&
                element.TryGetInt32(out var number)
                    ? number
                    : null;
        }
    }
}
