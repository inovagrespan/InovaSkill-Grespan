using Hangfire;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.BackgroundJobs;

[Queue(BackgroundJobQueues.RouteOptimization)]
public sealed class ProcessRouteOptimizationJob(
    IRouteOptimizationProcessingService processingService,
    ILogger<ProcessRouteOptimizationJob> logger)
{
    public async Task ExecuteAsync(Guid optimizationRunId, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["OptimizationRunId"] = optimizationRunId,
            ["Queue"] = BackgroundJobQueues.RouteOptimization
        });
        await processingService.ProcessAsync(optimizationRunId, cancellationToken);
    }
}
