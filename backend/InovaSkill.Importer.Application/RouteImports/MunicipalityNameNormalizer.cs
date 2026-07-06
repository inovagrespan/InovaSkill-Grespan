using System.Globalization;
using System.Text;

namespace InovaSkill.Importer.Application.RouteImports;

public static class MunicipalityNameNormalizer
{
    public static string Normalize(string value)
    {
        var compact = string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant()
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(compact.Length);
        foreach (var character in compact)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
