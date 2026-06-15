using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class JobRequestedEventHandler(
    ImportDbContext dbContext,
    IEnumerable<IJobHandler> jobHandlers) : IProcessingEventHandler
{
    public string EventType => ProcessingEventTypes.JobRequested;

    public async Task HandleAsync(ProcessingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var job = await dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == envelope.JobId, cancellationToken);
        if (job is null)
        {
            throw new InvalidOperationException($"Job {envelope.JobId} nao foi encontrado.");
        }

        if (job.Status == JobStatus.Cancelled)
        {
            return;
        }

        var handler = jobHandlers.FirstOrDefault(x => string.Equals(x.JobType, job.Type, StringComparison.OrdinalIgnoreCase));
        if (handler is null)
        {
            throw new InvalidOperationException($"No job handler registered for {job.Type}.");
        }

        await handler.HandleAsync(job, cancellationToken);

        if (job.Status == JobStatus.Processing)
        {
            job.MarkCompleted();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
