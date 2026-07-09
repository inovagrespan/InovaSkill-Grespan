using Hangfire;
using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.Detection;

[Queue(BackgroundJobQueues.Detectors)]
[AutomaticRetry(Attempts = 0)]
public sealed class ExecuteDetectionRunJob(
    IDetectionRunService detectionRunService,
    ILogger<ExecuteDetectionRunJob> logger)
{
    public async Task ExecuteAsync(
        Guid detectionRunId,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["DetectionRunId"] = detectionRunId,
            ["Queue"] = BackgroundJobQueues.Detectors
        });

        logger.LogInformation("Iniciando execução do detector (run {DetectionRunId}).", detectionRunId);
        await detectionRunService.ExecuteAsync(detectionRunId, cancellationToken);
        logger.LogInformation("Execução do detector (run {DetectionRunId}) finalizada.", detectionRunId);
    }
}
