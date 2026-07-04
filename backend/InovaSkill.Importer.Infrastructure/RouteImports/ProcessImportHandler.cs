using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class ProcessImportHandler(
    ImportDbContext dbContext,
    IEnumerable<IDataSourceProcessor> processors)
{
    private const int MaximumAttempts = 4;

    public async Task Handle(ProcessImport message, CancellationToken cancellationToken)
    {
        var job = await dbContext.JobExecutions
            .SingleAsync(x => x.Id == message.JobExecutionId, cancellationToken);
        var import = await dbContext.RouteImports
            .Include(x => x.DataSource)
            .SingleAsync(x => x.Id == message.ImportId, cancellationToken);

        job.Attempts++;
        job.StartedAt ??= DateTime.UtcNow;
        job.Status = JobExecutionStatus.Processing;
        job.ErrorMessage = null;
        import.Status = RouteImportStatus.Processing;
        import.StartedAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var processor = processors.SingleOrDefault(x =>
            string.Equals(x.SourceCode, import.DataSource!.Code, StringComparison.OrdinalIgnoreCase))
            ?? throw new StructuralImportException($"Não existe processador para a fonte '{import.DataSource!.Code}'.");

        try
        {
            await processor.ProcessAsync(import.Id, cancellationToken);
            job.Status = JobExecutionStatus.Completed;
            job.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (StructuralImportException exception)
        {
            import.Status = RouteImportStatus.Failed;
            import.FailureMessage = exception.Message;
            import.FinishedAt = DateTime.UtcNow;
            job.Status = JobExecutionStatus.Completed;
            job.FinishedAt = DateTime.UtcNow;
            job.ErrorMessage = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            job.ErrorMessage = exception.Message;
            if (job.Attempts >= MaximumAttempts)
            {
                job.Status = JobExecutionStatus.Failed;
                job.FinishedAt = DateTime.UtcNow;
                import.Status = RouteImportStatus.Failed;
                import.FailureMessage = "O processamento falhou após as tentativas automáticas.";
                import.FinishedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            job.Status = JobExecutionStatus.Retrying;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
