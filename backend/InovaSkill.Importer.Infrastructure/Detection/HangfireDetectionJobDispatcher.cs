using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.Detection;

public sealed class HangfireDetectionJobDispatcher(
    IBackgroundJobClient backgroundJobClient) : IDetectionJobDispatcher
{
    public string Enqueue(Guid detectionRunId) =>
        backgroundJobClient.Create(
            Job.FromExpression<ExecuteDetectionRunJob>(job =>
                job.ExecuteAsync(detectionRunId, CancellationToken.None)),
            new EnqueuedState(BackgroundJobQueues.Detectors));
}
