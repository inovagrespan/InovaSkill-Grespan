namespace InovaSkill.Importer.Application.Analytics;

public static class MovingAverageCalculator
{
    public static decimal? Calculate(IReadOnlyList<decimal> values, int window, int minimumPeriods)
    {
        if (values.Count < minimumPeriods || window <= 0 || minimumPeriods <= 0)
        {
            return null;
        }

        var count = Math.Min(window, values.Count);
        return values.Skip(values.Count - count).Take(count).Average();
    }
}
