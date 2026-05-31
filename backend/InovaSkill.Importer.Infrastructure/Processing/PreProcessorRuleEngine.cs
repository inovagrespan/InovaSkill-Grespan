using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Processing.Buffers;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class PreProcessorRuleEngine : IPreProcessorRuleEngine
{
    public PreProcessorExecutionResult Execute(ImportedRow row, IReadOnlyList<PreProcessorTemplateRule> rules)
    {
        if (rules.Count == 0)
        {
            return new PreProcessorExecutionResult(row, []);
        }

        var values = row.Values.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        var errors = new List<PreProcessorExecutionError>();

        foreach (var rule in rules.Where(x => x.IsEnabled).OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
        {
            try
            {
                ApplyRule(values, rule, errors);
            }
            catch (Exception ex)
            {
                errors.Add(new PreProcessorExecutionError(rule.Name, $"Rule '{rule.RuleType}' failed: {ex.Message}"));
            }
        }

        return new PreProcessorExecutionResult(new ImportedRow(row.RowNumber, values), errors);
    }

    private static void ApplyRule(Dictionary<string, string> values, PreProcessorTemplateRule rule, List<PreProcessorExecutionError> errors)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(rule.ConfigJson) ? "{}" : rule.ConfigJson);
        var root = doc.RootElement;
        var ruleType = rule.RuleType.Trim().ToLowerInvariant();

        switch (ruleType)
        {
            case "map_column":
                MapColumn(values, root);
                break;
            case "extract_regex":
                ExtractRegex(values, root);
                break;
            case "split_single_column":
                SplitSingleColumn(values, root);
                break;
            case "normalize_number":
                NormalizeNumber(values, root);
                break;
            case "normalize_date":
                NormalizeDate(values, root);
                break;
            case "convert_weight_to_kg":
                ConvertWeightToKg(values, root);
                break;
            case "validate_required":
                ValidateRequired(values, root, errors);
                break;
            case "validate_email":
                ValidateEmail(values, root, errors);
                break;
            case "validate_int":
                ValidateInt(values, root, errors);
                break;
            case "validate_decimal":
                ValidateDecimal(values, root, errors);
                break;
            case "validate_datetime":
                ValidateDateTime(values, root, errors);
                break;
            case "validate_regex":
                ValidateRegex(values, root, errors);
                break;
            default:
                throw new InvalidOperationException($"Unsupported rule type '{rule.RuleType}'.");
        }
    }

    private static void MapColumn(Dictionary<string, string> values, JsonElement config)
    {
        var from = GetRequiredString(config, "from");
        var to = GetRequiredString(config, "to");
        var overwrite = GetBool(config, "overwrite", true);

        if (!values.TryGetValue(from, out var sourceValue))
        {
            return;
        }

        if (!overwrite && values.TryGetValue(to, out var existing) && !string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        values[to] = sourceValue;
    }

    private static void ExtractRegex(Dictionary<string, string> values, JsonElement config)
    {
        var source = GetRequiredString(config, "source");
        var target = GetRequiredString(config, "target");
        var pattern = GetRequiredString(config, "pattern");
        var overwrite = GetBool(config, "overwrite", false);
        var group = GetString(config, "group") ?? "0";

        if (!values.TryGetValue(source, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        if (!overwrite && values.TryGetValue(target, out var existing) && !string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        var match = Regex.Match(raw, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return;
        }

        if (!int.TryParse(group, out var groupIndex))
        {
            groupIndex = 0;
        }

        if (groupIndex < 0 || groupIndex >= match.Groups.Count)
        {
            return;
        }

        values[target] = match.Groups[groupIndex].Value.Trim();
    }

    private static void SplitSingleColumn(Dictionary<string, string> values, JsonElement config)
    {
        var column = GetRequiredString(config, "column");
        var delimiter = GetString(config, "delimiter") ?? ";";
        var overwrite = GetBool(config, "overwrite", false);
        var headers = GetStringArray(config, "headers");

        if (headers.Count == 0)
        {
            throw new InvalidOperationException("headers is required for split_single_column.");
        }

        if (!values.TryGetValue(column, out var source) || string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        var parts = source.Split(delimiter);
        for (var i = 0; i < headers.Count && i < parts.Length; i++)
        {
            var target = headers[i];
            if (!overwrite && values.TryGetValue(target, out var existing) && !string.IsNullOrWhiteSpace(existing))
            {
                continue;
            }

            values[target] = parts[i].Trim();
        }
    }

    private static void NormalizeNumber(Dictionary<string, string> values, JsonElement config)
    {
        var column = GetRequiredString(config, "column");
        var target = GetString(config, "target") ?? column;
        var decimalSeparator = GetString(config, "decimalSeparator") ?? ",";
        var thousandSeparator = GetString(config, "thousandSeparator") ?? ".";

        if (!values.TryGetValue(column, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var normalized = raw.Trim().Replace(thousandSeparator, string.Empty).Replace(decimalSeparator, ".");
        if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        values[target] = value.ToString(CultureInfo.InvariantCulture);
    }

    private static void NormalizeDate(Dictionary<string, string> values, JsonElement config)
    {
        var column = GetRequiredString(config, "column");
        var target = GetString(config, "target") ?? column;
        var outputFormat = GetString(config, "outputFormat") ?? "yyyy-MM-dd";
        var formats = GetStringArray(config, "formats");

        if (!values.TryGetValue(column, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var trimmed = raw.Trim();
        if (formats.Count > 0)
        {
            if (!DateTime.TryParseExact(trimmed, formats.ToArray(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var configuredFormatParsed))
            {
                if (!UtcDateTimeParser.TryParse(trimmed, out configuredFormatParsed))
                {
                    return;
                }
            }

            values[target] = configuredFormatParsed.ToString(outputFormat, CultureInfo.InvariantCulture);
            return;
        }

        if (!UtcDateTimeParser.TryParse(trimmed, out var parsed))
        {
            return;
        }

        values[target] = parsed.ToString(outputFormat, CultureInfo.InvariantCulture);
    }

    private static void ConvertWeightToKg(Dictionary<string, string> values, JsonElement config)
    {
        var valueColumn = GetRequiredString(config, "valueColumn");
        var unitColumn = GetRequiredString(config, "unitColumn");
        var target = GetString(config, "target") ?? "weight_kg";

        if (!values.TryGetValue(valueColumn, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        if (!decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        var unit = values.TryGetValue(unitColumn, out var rawUnit) ? rawUnit?.Trim().ToLowerInvariant() : string.Empty;
        var kg = unit switch
        {
            "g" => value / 1000m,
            "kg" => value,
            _ => value
        };

        values[target] = kg.ToString(CultureInfo.InvariantCulture);
    }

    private static void ValidateRequired(Dictionary<string, string> values, JsonElement config, List<PreProcessorExecutionError> errors)
    {
        var column = GetRequiredString(config, "column");
        var message = GetString(config, "message") ?? "Field is required.";
        if (!values.TryGetValue(column, out var value) || string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new PreProcessorExecutionError(column, message));
        }
    }

    private static void ValidateEmail(Dictionary<string, string> values, JsonElement config, List<PreProcessorExecutionError> errors)
    {
        var column = GetRequiredString(config, "column");
        var message = GetString(config, "message") ?? "Invalid email value.";
        if (values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) && !value.Contains('@'))
        {
            errors.Add(new PreProcessorExecutionError(column, message));
        }
    }

    private static void ValidateInt(Dictionary<string, string> values, JsonElement config, List<PreProcessorExecutionError> errors)
    {
        var column = GetRequiredString(config, "column");
        var message = GetString(config, "message") ?? "Invalid int value.";
        if (values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _))
        {
            errors.Add(new PreProcessorExecutionError(column, message));
        }
    }

    private static void ValidateDecimal(Dictionary<string, string> values, JsonElement config, List<PreProcessorExecutionError> errors)
    {
        var column = GetRequiredString(config, "column");
        var message = GetString(config, "message") ?? "Invalid decimal value.";
        if (values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) && !decimal.TryParse(value, out _))
        {
            errors.Add(new PreProcessorExecutionError(column, message));
        }
    }

    private static void ValidateDateTime(Dictionary<string, string> values, JsonElement config, List<PreProcessorExecutionError> errors)
    {
        var column = GetRequiredString(config, "column");
        var message = GetString(config, "message") ?? "Invalid datetime value.";
        if (values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) && !UtcDateTimeParser.TryParse(value, out _))
        {
            errors.Add(new PreProcessorExecutionError(column, message));
        }
    }

    private static void ValidateRegex(Dictionary<string, string> values, JsonElement config, List<PreProcessorExecutionError> errors)
    {
        var column = GetRequiredString(config, "column");
        var pattern = GetRequiredString(config, "pattern");
        var message = GetString(config, "message") ?? "Invalid format.";
        if (values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            if (!Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                errors.Add(new PreProcessorExecutionError(column, message));
            }
        }
    }

    private static string GetRequiredString(JsonElement config, string propertyName)
    {
        var value = GetString(config, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Config property '{propertyName}' is required.");
        }

        return value;
    }

    private static string? GetString(JsonElement config, string propertyName)
    {
        if (!config.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool GetBool(JsonElement config, string propertyName, bool defaultValue)
    {
        if (!config.TryGetProperty(propertyName, out var value))
        {
            return defaultValue;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static List<string> GetStringArray(JsonElement config, string propertyName)
    {
        var list = new List<string>();
        if (!config.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                list.Add(item.GetString()!.Trim());
            }
        }

        return list;
    }
}



