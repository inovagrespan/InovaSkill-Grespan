using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OperationalJobQueue(
    ImportDbContext dbContext,
    IBackgroundJobDispatcher backgroundJobDispatcher) : IOperationalJobQueue
{
    public async Task<Guid?> TryQueueAsync(
        string jobType,
        Guid relatedEntityId,
        CancellationToken cancellationToken)
    {
        var definition = OperationalJobCatalog.All.SingleOrDefault(item => item.JobType == jobType)
            ?? throw new InvalidOperationException($"Job operacional desconhecido: {jobType}.");

        if (!definition.AllowConcurrentRuns)
        {
            var alreadyRunning = await dbContext.JobExecutions.AnyAsync(job =>
                job.JobType == jobType &&
                job.RelatedEntityId == relatedEntityId &&
                (job.Status == JobExecutionStatus.Queued ||
                 job.Status == JobExecutionStatus.Processing ||
                 job.Status == JobExecutionStatus.Retrying),
                cancellationToken);
            if (alreadyRunning) return null;
        }

        var job = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobType = jobType,
            Status = JobExecutionStatus.Queued,
            RelatedEntityId = relatedEntityId,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.JobExecutions.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            backgroundJobDispatcher.EnqueueOperationalJob(job.Id);
        }
        catch (Exception exception)
        {
            job.Status = JobExecutionStatus.Failed;
            job.ErrorMessage = $"Falha ao enfileirar o job no Hangfire: {exception.Message}";
            job.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return job.Id;
    }
}
