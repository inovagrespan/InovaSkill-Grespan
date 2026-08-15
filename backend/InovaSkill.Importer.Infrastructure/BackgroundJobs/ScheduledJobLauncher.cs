using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.BackgroundJobs;

public sealed class ScheduledJobLauncher(
    ImportDbContext db,
    IJobExecutionLauncher launcher) : IScheduledJobLauncher
{
    public async Task ExecuteAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var schedule = await db.JobSchedules.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == scheduleId, cancellationToken);
        if (schedule is null || !schedule.IsActive) return;
        await launcher.LaunchAsync(new JobLaunchRequest(
            schedule.JobType,
            schedule.ContractVersion,
            schedule.ParametersJson,
            JobExecutionTrigger.Schedule,
            schedule.CreatedByUserId,
            schedule.Id), cancellationToken);
    }
}
