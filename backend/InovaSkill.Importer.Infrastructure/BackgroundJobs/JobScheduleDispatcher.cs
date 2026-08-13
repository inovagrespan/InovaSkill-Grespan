using Hangfire;
using Hangfire.Common;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.BackgroundJobs;

public sealed class JobScheduleDispatcher(IRecurringJobManager recurringJobs) : IJobScheduleDispatcher
{
    private static string RecurringId(Guid id) => $"job-schedule:{id:N}";

    public void AddOrUpdate(Guid scheduleId, string cronExpression, string timeZoneId) =>
        recurringJobs.AddOrUpdate(
            RecurringId(scheduleId),
            Job.FromExpression<ScheduledJobLauncher>(job =>
                job.ExecuteAsync(scheduleId, CancellationToken.None)),
            cronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId) });

    public void Remove(Guid scheduleId) => recurringJobs.RemoveIfExists(RecurringId(scheduleId));
}
