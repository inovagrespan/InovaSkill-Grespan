using Hangfire;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.BackgroundJobs;

[Queue(BackgroundJobQueues.Imports)]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = [5, 30, 120], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class ProcessImportJob(
    IImportProcessingService processingService,
    ILogger<ProcessImportJob> logger)
{
    public async Task ExecuteAsync(
        Guid importId,
        Guid jobExecutionId,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["JobType"] = RouteImportCodes.JobType,
            ["ImportId"] = importId,
            ["JobExecutionId"] = jobExecutionId,
            ["Queue"] = BackgroundJobQueues.Imports
        });

        logger.LogInformation("Iniciando processamento de importação.");
        await processingService.ProcessAsync(importId, jobExecutionId, cancellationToken);
        logger.LogInformation("Processamento de importação finalizado.");
    }
}
