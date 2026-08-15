using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.BackgroundJobs;

public sealed class HangfireBackgroundJobDispatcher(
    IBackgroundJobClient backgroundJobClient) : IBackgroundJobDispatcher
{
    public string EnqueueImport(Guid importId, Guid jobExecutionId) =>
        backgroundJobClient.Create(
            Job.FromExpression<ProcessImportJob>(job =>
                job.ExecuteAsync(importId, jobExecutionId, CancellationToken.None)),
            new EnqueuedState(BackgroundJobQueues.Imports));

    public string EnqueueOperationalJob(Guid jobExecutionId) =>
        backgroundJobClient.Create(
            Job.FromExpression<ProcessOperationalJob>(job =>
                job.ExecuteAsync(jobExecutionId, CancellationToken.None)),
            new EnqueuedState(BackgroundJobQueues.Default));
}
