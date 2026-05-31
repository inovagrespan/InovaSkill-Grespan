namespace InovaSkill.Importer.Application.Analytics;

public enum PeriodComparisonGranularity
{
    Monthly,
    Weekly,
    Daily
}

public sealed record PeriodComparisonWindow(
    string Label,
    PeriodComparisonGranularity Granularity,
    DateTime CurrentFrom,
    DateTime CurrentToExclusive,
    DateTime PreviousFrom,
    DateTime PreviousToExclusive);

public sealed record PeriodComparisonMetrics(
    string Label,
    decimal CurrentValue,
    decimal PreviousValue,
    decimal? VariationPercent);

public static class PeriodComparisonCalculator
{
    private const int FirstDayOfMonth = 1;
    private const int DaysPerWeek = 7;
    private const int RollingComparisonDays = 30;
    private const int DateOnlyEndExclusiveOffsetDays = 1;

    public static IReadOnlyList<PeriodComparisonWindow> BuildWindows(DateTime referenceDate)
    {
        var reference = CustomerCalculators.NormalizeToUtc(referenceDate).Date;
        var currentMonthStart = new DateTime(reference.Year, reference.Month, FirstDayOfMonth, 0, 0, 0, DateTimeKind.Utc);
        var currentWeekStart = CustomerCalculators.StartOfWeekUtc(reference);
        var currentRollingStart = reference.AddDays(-(RollingComparisonDays - DateOnlyEndExclusiveOffsetDays));

        return new[]
        {
            new PeriodComparisonWindow(
                "Este mês vs mês anterior",
                PeriodComparisonGranularity.Monthly,
                currentMonthStart,
                currentMonthStart.AddMonths(1),
                currentMonthStart.AddMonths(-1),
                currentMonthStart),
            new PeriodComparisonWindow(
                "Esta semana vs semana anterior",
                PeriodComparisonGranularity.Weekly,
                currentWeekStart,
                currentWeekStart.AddDays(DaysPerWeek),
                currentWeekStart.AddDays(-DaysPerWeek),
                currentWeekStart),
            new PeriodComparisonWindow(
                "Últimos 30 dias vs 30 dias anteriores",
                PeriodComparisonGranularity.Daily,
                currentRollingStart,
                reference.AddDays(DateOnlyEndExclusiveOffsetDays),
                currentRollingStart.AddDays(-RollingComparisonDays),
                currentRollingStart)
        };
    }

    public static PeriodComparisonMetrics BuildMetrics(string label, decimal currentValue, decimal previousValue)
    {
        return new PeriodComparisonMetrics(
            label,
            currentValue,
            previousValue,
            CustomerCalculators.CalculateVariationPercent(currentValue, previousValue));
    }
}
