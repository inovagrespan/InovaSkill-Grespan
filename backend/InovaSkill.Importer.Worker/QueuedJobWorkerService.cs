using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Worker;

public sealed class QueuedJobWorkerService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<QueuedJobWorkerService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
                var jobHandlers = scope.ServiceProvider.GetServices<IJobHandler>().ToArray();
                var workerId = $"{Environment.MachineName}:queued-jobs";

                var job = await TryClaimNextJobAsync(dbContext, workerId, stoppingToken);
                if (job is null)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                    continue;
                }

                var handler = jobHandlers.FirstOrDefault(x => string.Equals(x.JobType, job.Type, StringComparison.OrdinalIgnoreCase));
                if (handler is null)
                {
                    job.MarkFailed($"No job handler registered for {job.Type}.");
                    job.LockedBy = string.Empty;
                    job.LockedAt = null;
                    await dbContext.SaveChangesAsync(stoppingToken);
                    continue;
                }

                logger.LogInformation("Processing queued job {JobId} ({JobType}).", job.Id, job.Type);

                if (job.Status == JobStatus.Queued)
                {
                    job.MarkProcessing();
                    await dbContext.SaveChangesAsync(stoppingToken);
                }

                await handler.HandleAsync(job, stoppingToken);

                if (job.Status == JobStatus.Processing)
                {
                    job.MarkCompleted();
                }

                job.LockedBy = string.Empty;
                job.LockedAt = null;
                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing queued jobs directly from database.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private static async Task<Job?> TryClaimNextJobAsync(
        ImportDbContext dbContext,
        string workerId,
        CancellationToken cancellationToken)
    {
        var staleBefore = DateTime.UtcNow - LockTimeout;

        var job = await dbContext.Jobs
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(
                x => x.Status == JobStatus.Queued &&
                    (x.LockedAt == null || x.LockedAt < staleBefore || x.LockedBy == workerId),
                cancellationToken);

        if (job is null)
        {
            return null;
        }

        job.LockedBy = workerId;
        job.LockedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }
}
