using InovaSkill.Importer.Application.Abstractions;
using System.Text.RegularExpressions;

namespace InovaSkill.Importer.Infrastructure.Processing.TransformRules;

public sealed class OnlyDigitsRule : ITransformRule
{
    public string Code => "OnlyDigits";

    public object? Apply(object? value, string? parametersJson)
    {
        if (value is null)
        {
            return null;
        }

        var raw = value.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        return Regex.Replace(raw, "[^0-9]", string.Empty);
    }
}
