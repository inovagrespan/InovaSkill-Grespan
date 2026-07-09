using Hangfire;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.BackgroundJobs;

[Queue(BackgroundJobQueues.Default)]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = [5, 30, 120], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class ProcessOperationalJob(
    IOperationalJobProcessingService processingService,
    ILogger<ProcessOperationalJob> logger)
{
    public async Task ExecuteAsync(
        Guid jobExecutionId,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["JobExecutionId"] = jobExecutionId,
            ["Queue"] = BackgroundJobQueues.Default
        });

        logger.LogInformation("Iniciando job operacional.");
        await processingService.ProcessAsync(jobExecutionId, cancellationToken);
        logger.LogInformation("Job operacional finalizado.");
    }
}
