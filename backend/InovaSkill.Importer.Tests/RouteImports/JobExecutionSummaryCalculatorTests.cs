using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class JobExecutionSummaryCalculatorTests
{
    private static readonly DateTime Now = new(2026, 7, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Calculate_UsesStatusPeriodFormulaAndCompletedDuration()
    {
        var jobs = new[]
        {
            Job(JobExecutionStatus.Queued),
            Job(JobExecutionStatus.Processing),
            Job(JobExecutionStatus.Retrying),
            Job(JobExecutionStatus.Completed, Now.AddMinutes(-10), Now.AddMinutes(-8)),
            Job(JobExecutionStatus.Completed, Now.AddMinutes(-5), Now.AddMinutes(-4)),
            Job(JobExecutionStatus.Failed, Now.AddMinutes(-3), Now.AddMinutes(-2)),
            Job(JobExecutionStatus.Completed, Now.AddHours(-26), Now.AddHours(-25)),
        };

        var result = JobExecutionSummaryCalculator.Calculate(jobs, Now);

        Assert.Equal(1, result.QueuedNow);
        Assert.Equal(2, result.ProcessingNow);
        Assert.Equal(2, result.CompletedLast24Hours);
        Assert.Equal(1, result.FailedLast24Hours);
        Assert.Equal(66.67m, result.SuccessRatePercent);
        Assert.Equal(1_260d, result.AverageProcessingSeconds);
    }

    [Fact]
    public void Calculate_EmptyBase_ReturnsZeroAndIgnoresInvalidDurations()
    {
        var jobs = new[] { Job(JobExecutionStatus.Completed, Now, Now.AddSeconds(-1)) };

        var result = JobExecutionSummaryCalculator.Calculate(jobs, Now.AddDays(2));

        Assert.Equal(0m, result.SuccessRatePercent);
        Assert.Equal(0d, result.AverageProcessingSeconds);
    }

    private static JobExecution Job(JobExecutionStatus status, DateTime? startedAt = null, DateTime? finishedAt = null) =>
        new() { Status = status, StartedAt = startedAt, FinishedAt = finishedAt };
}
