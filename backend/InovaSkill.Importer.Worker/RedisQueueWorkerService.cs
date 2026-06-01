using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;

namespace InovaSkill.Importer.Worker;

public sealed class RedisQueueWorkerService(
    IProcessingEventConsumer eventConsumer,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RedisQueueWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromSeconds(2);
        var workerId = $"{Environment.MachineName}:import";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateHeartbeatAsync(workerId, null, "Aguardando job", isIdle: true, stoppingToken);
                var envelope = await eventConsumer.DequeueAsync(stoppingToken);
                if (envelope is null)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                    continue;
                }

                logger.LogInformation("Dequeued event {EventType} for job {JobId} from Redis queue.", envelope.EventType, envelope.JobId);
                await UpdateHeartbeatAsync(workerId, envelope.JobId, $"Processando {envelope.EventType}", isIdle: false, stoppingToken);
                using var scope = serviceScopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<InovaSkill.Importer.Infrastructure.Processing.ProcessingEventDispatcher>();
                await dispatcher.DispatchAsync(envelope, stoppingToken);
                await IncrementProcessedJobsAsync(workerId, stoppingToken);
                logger.LogInformation("Finished event {EventType} for job {JobId}.", envelope.EventType, envelope.JobId);
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
        heartbeat.CurrentTask = "Aguardando job";
        heartbeat.IdleSinceAt = DateTime.UtcNow;
        heartbeat.ProcessedJobsToday++;
        await db.SaveChangesAsync(cancellationToken);
    }
}
