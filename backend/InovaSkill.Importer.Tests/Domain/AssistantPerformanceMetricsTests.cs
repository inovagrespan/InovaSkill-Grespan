using InovaSkill.Importer.Domain;

namespace InovaSkill.Importer.Tests.Domain;

public sealed class AssistantPerformanceMetricsTests
{
    [Fact]
    public void Calculate_CoversAverageMedianP95LimitsFailuresAndReduction()
    {
        var result = AssistantPerformanceMetrics.Calculate(5, 1, [1_000, 2_000, 3_000, 10_000]);

        Assert.Equal(5, result.TotalQueries);
        Assert.Equal(4, result.CompletedQueries);
        Assert.Equal(1, result.FailedQueries);
        Assert.Equal(4_000, result.AverageMilliseconds);
        Assert.Equal(2_500, result.MedianMilliseconds);
        Assert.Equal(10_000, result.P95Milliseconds);
        Assert.Equal(1_000, result.MinimumMilliseconds);
        Assert.Equal(10_000, result.MaximumMilliseconds);
        Assert.Equal(99.33m, result.ReductionAgainstTenMinutesPercent);
        Assert.Equal(99.94m, result.ReductionAgainstTwoHoursPercent);
    }

    [Fact]
    public void Calculate_ReturnsNullMetricsWithoutCompletedQueries()
    {
        var result = AssistantPerformanceMetrics.Calculate(2, 2, []);
        Assert.Null(result.AverageMilliseconds);
        Assert.Null(result.ReductionAgainstTenMinutesPercent);
    }

    [Fact]
    public void Calculate_DoesNotReportNegativeReduction()
    {
        var result = AssistantPerformanceMetrics.Calculate(1, 0, [AssistantPerformanceMetrics.TwoHoursMilliseconds * 2]);
        Assert.Equal(0m, result.ReductionAgainstTenMinutesPercent);
        Assert.Equal(0m, result.ReductionAgainstTwoHoursPercent);
    }
}
