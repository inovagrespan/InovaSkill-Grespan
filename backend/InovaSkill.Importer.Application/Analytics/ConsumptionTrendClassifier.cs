namespace InovaSkill.Importer.Application.Analytics;

public static class ConsumptionTrendClassifier
{
    private const int RequiredMonthsForTrend = 6;
    private const int TrendComparisonWindowMonths = 3;
    private const decimal TrendChangeThresholdRatio = 0.05m;

    public const string Growth = "Crescimento";
    public const string Drop = "Queda";
    public const string Stable = "Estabilidade";

    public static string Classify(IReadOnlyList<decimal> quantities)
    {
        if (quantities.Count < RequiredMonthsForTrend)
        {
            return Stable;
        }

        var recent = quantities.Skip(quantities.Count - TrendComparisonWindowMonths).Take(TrendComparisonWindowMonths).Average();
        var previous = quantities.Skip(quantities.Count - RequiredMonthsForTrend).Take(TrendComparisonWindowMonths).Average();
        if (previous <= 0)
        {
            return recent > 0 ? Growth : Stable;
        }

        var ratio = (recent - previous) / previous;
        if (ratio >= TrendChangeThresholdRatio) return Growth;
        if (ratio <= -TrendChangeThresholdRatio) return Drop;
        return Stable;
    }
}
