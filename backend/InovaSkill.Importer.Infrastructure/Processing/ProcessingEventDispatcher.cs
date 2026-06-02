using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class ProcessingEventDispatcher(
    IEnumerable<IProcessingEventHandler> handlers,
    IProcessingEventPublisher eventPublisher,
    IProcessingDeadLetterQueue deadLetterQueue,
    ImportDbContext dbContext)
{
    private const int MaxRetryCount = 3;
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(10);

    public async Task DispatchAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var eventLog = new ProcessingJobEventLog
        {
            FileJobId = envelope.JobId,
            EventType = envelope.EventType,
            Status = "processing",
            CorrelationId = envelope.CorrelationId,
            RetryCount = envelope.RetryCount,
            CreatedAt = envelope.CreatedAt
        };
        dbContext.ProcessingJobEventLogs.Add(eventLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        var handler = handlers.FirstOrDefault(x => string.Equals(x.EventType, envelope.EventType, StringComparison.OrdinalIgnoreCase));
        if (handler is null)
        {
            await MoveToDeadLetterAsync(envelope, eventLog, $"No handler registered for {envelope.EventType}.", cancellationToken);
            return;
        }

        var workerId = Environment.MachineName;
        if (!await TryClaimJobAsync(envelope.JobId, workerId, cancellationToken))
        {
            eventLog.Status = "skipped";
            eventLog.ProcessedAt = DateTime.UtcNow;
            eventLog.ErrorMessage = "Job ja esta bloqueado por outro worker.";
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            await handler.HandleAsync(envelope, cancellationToken);
            eventLog.Status = "processed";
            eventLog.ProcessedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsNonRetryable(ex))
        {
            await MoveToDeadLetterAsync(envelope, eventLog, ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            if (envelope.RetryCount + 1 >= MaxRetryCount)
            {
                await MoveToDeadLetterAsync(envelope, eventLog, ex.Message, cancellationToken);
                return;
            }

            var retry = envelope with
            {
                RetryCount = envelope.RetryCount + 1,
                CreatedAt = DateTime.UtcNow
            };

            eventLog.Status = "retry_scheduled";
            eventLog.ProcessedAt = DateTime.UtcNow;
            eventLog.ErrorMessage = Truncate(ex.Message);
            await dbContext.SaveChangesAsync(cancellationToken);
            await eventPublisher.PublishAsync(retry, cancellationToken);
        }
        finally
        {
            await ReleaseJobAsync(envelope.JobId, workerId, cancellationToken);
        }
    }

    private async Task<bool> TryClaimJobAsync(long jobId, string workerId, CancellationToken cancellationToken)
    {
        var staleBefore = DateTime.UtcNow - LockTimeout;
        try
        {
            var affectedRows = await dbContext.FileJobs
                .Where(x => x.Id == jobId && (x.LockedAt == null || x.LockedAt < staleBefore || x.LockedBy == workerId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LockedBy, workerId)
                    .SetProperty(x => x.LockedAt, DateTime.UtcNow),
                    cancellationToken);

            return affectedRows == 1;
        }
        catch (InvalidOperationException)
        {
            var job = await dbContext.FileJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
            if (job is null || (job.LockedAt is not null && job.LockedAt >= staleBefore && job.LockedBy != workerId))
            {
                return false;
            }

            job.LockedBy = workerId;
            job.LockedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    private async Task ReleaseJobAsync(long jobId, string workerId, CancellationToken cancellationToken)
    {
        var job = await dbContext.FileJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null || !string.Equals(job.LockedBy, workerId, StringComparison.Ordinal))
        {
            return;
        }

        job.LockedBy = string.Empty;
        job.LockedAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MoveToDeadLetterAsync(
        ProcessingEventEnvelope envelope,
        ProcessingJobEventLog eventLog,
        string reason,
        CancellationToken cancellationToken)
    {
        eventLog.Status = "dead_letter";
        eventLog.ProcessedAt = DateTime.UtcNow;
        eventLog.ErrorMessage = Truncate(reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        await deadLetterQueue.PublishDeadLetterAsync(envelope, reason, cancellationToken);
    }

    private static bool IsNonRetryable(InvalidOperationException ex)
    {
        return ex.Message.Contains("validacao", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("nao suport", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value)
    {
        return value.Length <= 1024 ? value : value[..1024];
    }
}
