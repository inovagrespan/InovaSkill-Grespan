using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class ProcessOperationalJobHandler(
    ImportDbContext dbContext,
    IEnumerable<IOperationalJobProcessor> processors)
{
    private const int MaximumAttempts = 4;

    public async Task Handle(ProcessOperationalJob message, CancellationToken cancellationToken)
    {
        var job = await dbContext.JobExecutions
            .SingleAsync(x => x.Id == message.JobExecutionId, cancellationToken);

        if (job.Status is JobExecutionStatus.Completed or JobExecutionStatus.Failed)
        {
            return;
        }

        job.Attempts++;
        job.StartedAt ??= DateTime.UtcNow;
        job.Status = JobExecutionStatus.Processing;
        job.ErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        var processor = processors.SingleOrDefault(item =>
            string.Equals(item.JobType, job.JobType, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Não existe processador operacional para o job '{job.JobType}'.");

        try
        {
            await processor.ProcessAsync(job.RelatedEntityId, cancellationToken);
            dbContext.ChangeTracker.Clear();
            job = await dbContext.JobExecutions
                .SingleAsync(x => x.Id == message.JobExecutionId, cancellationToken);
            job.Status = JobExecutionStatus.Completed;
            job.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            job = await dbContext.JobExecutions
                .SingleAsync(x => x.Id == message.JobExecutionId, cancellationToken);
            job.ErrorMessage = exception.Message;
            if (job.Attempts >= MaximumAttempts)
            {
                job.Status = JobExecutionStatus.Failed;
                job.FinishedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            job.Status = JobExecutionStatus.Retrying;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
