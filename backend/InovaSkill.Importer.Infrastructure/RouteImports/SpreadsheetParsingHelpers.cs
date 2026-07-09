using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

internal static partial class SpreadsheetParsingHelpers
{
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");

    public static string Compact(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string NormalizeHeader(string value) => MunicipalityNameNormalizer.Normalize(value)
        .Replace(".", string.Empty, StringComparison.Ordinal)
        .Replace("*", string.Empty, StringComparison.Ordinal);

    public static bool TryReadDecimal(IXLCell cell, out decimal value)
    {
        value = 0;
        if (cell.IsEmpty()) return true;
        if (cell.TryGetValue<decimal>(out value)) return true;
        var raw = Compact(cell.GetFormattedString());
        if (string.IsNullOrWhiteSpace(raw)) return true;
        return decimal.TryParse(raw, NumberStyles.Number, BrazilianCulture, out value) ||
               decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryReadDailyQuantity(IXLCell cell, out decimal value, out string raw)
    {
        value = 0;
        raw = Compact(cell.GetFormattedString());
        if (cell.IsEmpty()) return true;
        if (!cell.HasFormula) return TryReadDecimal(cell, out value);
        var formula = cell.FormulaA1.Trim();
        raw = "=" + formula;
        if (FormulaErrorPattern().IsMatch(formula)) return false;
        if (TryEvaluateSimpleArithmetic(formula, out value)) return true;
        if (decimal.TryParse(cell.CachedValue.ToString(), NumberStyles.Number, BrazilianCulture, out var cached) ||
            decimal.TryParse(cell.CachedValue.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out cached))
        {
            value = cached;
            return true;
        }
        return false;
    }

    private static bool TryEvaluateSimpleArithmetic(string formula, out decimal result)
    {
        result = 0;
        var expression = formula.TrimStart('=').Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!SimpleArithmeticPattern().IsMatch(expression)) return false;
        var sign = 1m;
        foreach (Match match in ArithmeticTermPattern().Matches(expression))
        {
            var token = match.Value;
            if (token == "+") { sign = 1m; continue; }
            if (token == "-") { sign = -1m; continue; }
            if (!decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) &&
                !decimal.TryParse(token, NumberStyles.Number, BrazilianCulture, out number))
                return false;
            result += sign * number;
        }
        return true;
    }

    [GeneratedRegex(@"#REF!|#VALUE!|#DIV/0!|#N/A", RegexOptions.IgnoreCase)]
    private static partial Regex FormulaErrorPattern();

    [GeneratedRegex(@"^[+-]?\d+([,.]\d+)?([+-]\d+([,.]\d+)?)*$")]
    private static partial Regex SimpleArithmeticPattern();

    [GeneratedRegex(@"[+-]|\d+([,.]\d+)?")]
    private static partial Regex ArithmeticTermPattern();
}
