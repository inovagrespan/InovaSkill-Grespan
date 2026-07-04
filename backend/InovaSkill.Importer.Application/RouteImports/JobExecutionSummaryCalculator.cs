using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Application.RouteImports;

public sealed record JobExecutionSummary(
    int QueuedNow,
    int ProcessingNow,
    int CompletedLast24Hours,
    int FailedLast24Hours,
    decimal SuccessRatePercent,
    double AverageProcessingSeconds);

public static class JobExecutionSummaryCalculator
{
    public static JobExecutionSummary Calculate(IReadOnlyCollection<JobExecution> jobs, DateTime now)
    {
        var since = now.AddHours(-24);
        var completed = jobs.Count(x => x.Status == JobExecutionStatus.Completed && x.FinishedAt >= since);
        var failed = jobs.Count(x => x.Status == JobExecutionStatus.Failed && x.FinishedAt >= since);
        var finalized = completed + failed;
        var durations = jobs
            .Where(x => x.Status == JobExecutionStatus.Completed
                && x.StartedAt.HasValue
                && x.FinishedAt >= x.StartedAt)
            .Select(x => (x.FinishedAt!.Value - x.StartedAt!.Value).TotalSeconds)
            .ToArray();

        return new JobExecutionSummary(
            jobs.Count(x => x.Status == JobExecutionStatus.Queued),
            jobs.Count(x => x.Status is JobExecutionStatus.Processing or JobExecutionStatus.Retrying),
            completed,
            failed,
            finalized == 0 ? 0m : Math.Round((decimal)completed / finalized * 100m, 2),
            durations.Length == 0 ? 0d : Math.Round(durations.Average(), 2));
    }
}
