namespace InovaSkill.Importer.Domain;

public sealed record AssistantPerformanceSummary(
    int TotalQueries,
    int CompletedQueries,
    int FailedQueries,
    long? AverageMilliseconds,
    long? MedianMilliseconds,
    long? P95Milliseconds,
    long? MinimumMilliseconds,
    long? MaximumMilliseconds,
    decimal? ReductionAgainstTenMinutesPercent,
    decimal? ReductionAgainstTwoHoursPercent);

public static class AssistantPerformanceMetrics
{
    public const long TenMinutesMilliseconds = 600_000;
    public const long TwoHoursMilliseconds = 7_200_000;

    public static AssistantPerformanceSummary Calculate(int totalQueries, int failedQueries, IEnumerable<long> completedDurations)
    {
        var durations = completedDurations.Where(value => value >= 0).OrderBy(value => value).ToArray();
        if (durations.Length == 0)
            return new(totalQueries, 0, failedQueries, null, null, null, null, null, null, null);

        var average = (long)Math.Round(durations.Average(), MidpointRounding.AwayFromZero);
        var middle = durations.Length / 2;
        var median = durations.Length % 2 == 0
            ? (long)Math.Round((durations[middle - 1] + durations[middle]) / 2m, MidpointRounding.AwayFromZero)
            : durations[middle];
        var p95Index = Math.Max(0, (int)Math.Ceiling(durations.Length * 0.95m) - 1);
        return new(
            totalQueries,
            durations.Length,
            failedQueries,
            average,
            median,
            durations[p95Index],
            durations[0],
            durations[^1],
            Reduction(average, TenMinutesMilliseconds),
            Reduction(average, TwoHoursMilliseconds));
    }

    private static decimal Reduction(long durationMilliseconds, long baselineMilliseconds) =>
        Math.Clamp(Math.Round((baselineMilliseconds - durationMilliseconds) * 100m / baselineMilliseconds, 2, MidpointRounding.AwayFromZero), 0m, 100m);
}
