using InovaSkill.Importer.Application.Abstractions;

namespace InovaSkill.Importer.Worker;

public sealed class RedisQueueWorkerService(
    IFileJobQueue fileJobQueue,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RedisQueueWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromSeconds(2);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await fileJobQueue.DequeueAsync(stoppingToken);
                if (!jobId.HasValue)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                    continue;
                }

                logger.LogInformation("Dequeued job {JobId} from Redis queue.", jobId.Value);
                using var scope = serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IFileImportPipelineProcessor>();
                await processor.ProcessJobAsync(jobId.Value, stoppingToken);
                logger.LogInformation("Finished job {JobId}.", jobId.Value);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error consuming Redis queue in worker loop.");
                await Task.Delay(idleDelay, stoppingToken);
            }
        }
    }
}
