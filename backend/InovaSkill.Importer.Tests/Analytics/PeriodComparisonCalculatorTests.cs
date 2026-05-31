using InovaSkill.Importer.Application.Analytics;

namespace InovaSkill.Importer.Tests.Analytics;

public sealed class PeriodComparisonCalculatorTests
{
    [Fact]
    public void ShouldBuildMonthlyWeeklyAndRollingThirtyDayWindows()
    {
        var result = PeriodComparisonCalculator.BuildWindows(new DateTime(2026, 5, 30, 15, 0, 0, DateTimeKind.Utc));

        Assert.Equal(3, result.Count);

        Assert.Equal("Este mês vs mês anterior", result[0].Label);
        Assert.Equal(PeriodComparisonGranularity.Monthly, result[0].Granularity);
        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), result[0].CurrentFrom);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), result[0].CurrentToExclusive);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), result[0].PreviousFrom);
        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), result[0].PreviousToExclusive);

        Assert.Equal("Esta semana vs semana anterior", result[1].Label);
        Assert.Equal(PeriodComparisonGranularity.Weekly, result[1].Granularity);
        Assert.Equal(new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), result[1].CurrentFrom);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), result[1].CurrentToExclusive);
        Assert.Equal(new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc), result[1].PreviousFrom);
        Assert.Equal(new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), result[1].PreviousToExclusive);

        Assert.Equal("Últimos 30 dias vs 30 dias anteriores", result[2].Label);
        Assert.Equal(PeriodComparisonGranularity.Daily, result[2].Granularity);
        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), result[2].CurrentFrom);
        Assert.Equal(new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc), result[2].CurrentToExclusive);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), result[2].PreviousFrom);
        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), result[2].PreviousToExclusive);
    }

    [Fact]
    public void ShouldCalculateVariationForComparisonMetrics()
    {
        var result = PeriodComparisonCalculator.BuildMetrics("Período", currentValue: 300m, previousValue: 100m);

        Assert.Equal("Período", result.Label);
        Assert.Equal(300m, result.CurrentValue);
        Assert.Equal(100m, result.PreviousValue);
        Assert.Equal(200m, result.VariationPercent);
    }
}
