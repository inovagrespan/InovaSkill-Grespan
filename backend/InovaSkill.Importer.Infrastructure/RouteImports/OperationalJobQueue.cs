using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OperationalJobQueue(
    ImportDbContext dbContext,
    IMessageBus messageBus) : IOperationalJobQueue
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
        await messageBus.PublishAsync(new ProcessOperationalJob(job.Id));
        return job.Id;
    }
}
