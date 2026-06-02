using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Worker;

public sealed class PostImportWorkerService(
    IPostImportJobQueue postImportJobQueue,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PostImportWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromSeconds(2);
        var workerId = $"{Environment.MachineName}:post-import";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateHeartbeatAsync(workerId, null, "Aguardando resumo", isIdle: true, stoppingToken);
                var job = await postImportJobQueue.DequeueAsync(stoppingToken);
                if (job is null)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                    continue;
                }

                await UpdateHeartbeatAsync(workerId, job.FileJobId, $"Processando {job.JobType}", isIdle: false, stoppingToken);
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
                await IncrementProcessedJobsAsync(workerId, stoppingToken);
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

    private async Task UpdateHeartbeatAsync(string workerId, long? currentJobId, string currentTask, bool isIdle, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
        var heartbeat = await db.WorkerHeartbeats.FindAsync([workerId], cancellationToken);
        if (heartbeat is null)
        {
            heartbeat = new WorkerHeartbeat { WorkerId = workerId };
            db.WorkerHeartbeats.Add(heartbeat);
        }

        heartbeat.LastSeenAt = DateTime.UtcNow;
        heartbeat.CurrentJobId = currentJobId;
        heartbeat.CurrentTask = currentTask;
        heartbeat.IdleSinceAt = isIdle ? heartbeat.IdleSinceAt ?? DateTime.UtcNow : null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task IncrementProcessedJobsAsync(string workerId, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
        var heartbeat = await db.WorkerHeartbeats.FindAsync([workerId], cancellationToken);
        if (heartbeat is null)
        {
            heartbeat = new WorkerHeartbeat { WorkerId = workerId };
            db.WorkerHeartbeats.Add(heartbeat);
        }

        heartbeat.LastSeenAt = DateTime.UtcNow;
        heartbeat.CurrentJobId = null;
        heartbeat.CurrentTask = "Aguardando resumo";
        heartbeat.IdleSinceAt = DateTime.UtcNow;
        heartbeat.ProcessedJobsToday++;
        await db.SaveChangesAsync(cancellationToken);
    }
}
