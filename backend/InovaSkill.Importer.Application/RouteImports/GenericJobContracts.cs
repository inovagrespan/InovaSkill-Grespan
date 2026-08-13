using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Application.RouteImports;

public static class GenericJobPolicy
{
    public const int MaximumJsonBytes = 1024 * 1024;
    public const string DefaultTimeZoneId = "America/Sao_Paulo";
}

public sealed record JobLaunchRequest(
    string JobType,
    int ContractVersion,
    string ParametersJson,
    JobExecutionTrigger Trigger,
    long? RequestedByUserId = null,
    Guid? ScheduleId = null,
    Guid? RetriedFromJobExecutionId = null);

public sealed record JobLaunchResult(Guid JobExecutionId, string Status);

public interface IJobExecutionLauncher
{
    Task<JobLaunchResult> LaunchAsync(JobLaunchRequest request, CancellationToken cancellationToken);
}

public interface IJobScheduleDispatcher
{
    void AddOrUpdate(Guid scheduleId, string cronExpression, string timeZoneId);
    void Remove(Guid scheduleId);
}

public interface IScheduledJobLauncher
{
    Task ExecuteAsync(Guid scheduleId, CancellationToken cancellationToken);
}
