using InovaSkill.Importer.Application.Abstractions;

namespace InovaSkill.Importer.Worker;

public sealed class PostImportWorkerService(
    IPostImportJobQueue postImportJobQueue,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PostImportWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromSeconds(2);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await postImportJobQueue.DequeueAsync(stoppingToken);
                if (job is null)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                    continue;
                }

                using var scope = serviceScopeFactory.CreateScope();
                var processors = scope.ServiceProvider.GetServices<IPostImportProcessor>();
                var processor = processors.FirstOrDefault(x => x.JobType == job.JobType);
                if (processor is null)
                {
                    logger.LogWarning("No post-import processor configured for job type {JobType}.", job.JobType);
                    continue;
                }

                logger.LogInformation("Dequeued post-import job for file job {FileJobId} ({JobType}).", job.FileJobId, job.JobType);
                await processor.ProcessAsync(job.FileJobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing post-import job.");
                await Task.Delay(idleDelay, stoppingToken);
            }
        }
    }
}
