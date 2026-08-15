using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OperationalJobProcessingService(
    ImportDbContext dbContext,
    IEnumerable<IOperationalJobProcessor> processors) : IOperationalJobProcessingService
{
    private const int MaximumAttempts = 4;

    public async Task ProcessAsync(
        Guid jobExecutionId,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.JobExecutions
            .SingleAsync(x => x.Id == jobExecutionId, cancellationToken);

        if (job.Status is JobExecutionStatus.Completed or JobExecutionStatus.Failed or JobExecutionStatus.Cancelled)
        {
            return;
        }
        if (job.CancellationRequestedAt.HasValue || job.Status == JobExecutionStatus.Cancelled)
        {
            job.Status = JobExecutionStatus.Cancelled;
            job.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        job.Attempts++;
        job.StartedAt ??= DateTime.UtcNow;
        job.Status = JobExecutionStatus.Processing;
        job.ErrorMessage = null;
        job.ProgressPercent = 1;
        job.ProgressMessage = "Processando";
        await dbContext.SaveChangesAsync(cancellationToken);

        var processor = processors.SingleOrDefault(item =>
            string.Equals(item.JobType, job.JobType, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Não existe processador operacional para o job '{job.JobType}'.");

        try
        {
            if (processor is IProgressReportingOperationalJobProcessor progressReportingProcessor)
                await progressReportingProcessor.ProcessAsync(job.RelatedEntityId, job.Id, cancellationToken);
            else
                await processor.ProcessAsync(job.RelatedEntityId, cancellationToken);
            dbContext.ChangeTracker.Clear();
            job = await dbContext.JobExecutions
                .SingleAsync(x => x.Id == jobExecutionId, cancellationToken);
            if (job.CancellationRequestedAt.HasValue)
            {
                job.Status = JobExecutionStatus.Cancelled;
                job.ProgressMessage = "Cancelado";
                job.FinishedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            job.Status = JobExecutionStatus.Completed;
            job.ProgressPercent = 100;
            job.ProgressMessage = job.ResultJson is null
                ? "Concluído"
                : $"Concluído: {job.ProgressMessage}";
            job.ResultJson ??= JsonSerializer.Serialize(new { relatedEntityId = job.RelatedEntityId, completed = true });
            job.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            job = await dbContext.JobExecutions
                .SingleAsync(x => x.Id == jobExecutionId, cancellationToken);
            job.ErrorMessage = exception.Message;
            if (job.Attempts >= MaximumAttempts)
            {
                job.Status = JobExecutionStatus.Failed;
                job.ProgressMessage = "Falha";
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
