using InovaSkill.Importer.Application.Abstractions;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InovaSkill.Importer.Infrastructure.Processing.TransformRules;

public sealed partial class RemoveSpecialCharactersRule : ITransformRule
{
    public string Code => "RemoveSpecialCharacters";

    public object? Apply(object? value, string? parametersJson)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.ToString()?.Normalize(NormalizationForm.FormD) ?? string.Empty;
        var withoutDiacritics = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                withoutDiacritics.Append(character);
            }
        }

        return SpecialCharactersRegex()
            .Replace(withoutDiacritics.ToString().Normalize(NormalizationForm.FormC), string.Empty)
            .Trim();
    }

    [GeneratedRegex(@"[^\p{L}\p{N}\s._@-]+", RegexOptions.Compiled)]
    private static partial Regex SpecialCharactersRegex();
}
