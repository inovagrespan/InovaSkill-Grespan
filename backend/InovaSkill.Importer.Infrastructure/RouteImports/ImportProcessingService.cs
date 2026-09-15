using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class ImportProcessingService(
    ImportDbContext dbContext,
    IEnumerable<IDataSourceProcessor> processors,
    IImportLifecycleService importLifecycle,
    IRouteCustomerAssignmentSynchronizer routeCustomerAssignments,
    IOperationalJobQueue operationalJobQueue) : IImportProcessingService
{
    private const int MaximumAttempts = 4;
    private const int MaximumPersistedErrorMessageLength = 1024;

    public async Task ProcessAsync(
        Guid importId,
        Guid jobExecutionId,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.JobExecutions
            .SingleAsync(x => x.Id == jobExecutionId, cancellationToken);
        var import = await dbContext.RouteImports
            .Include(x => x.DataSource)
            .SingleAsync(x => x.Id == importId, cancellationToken);
        if (import.Status == RouteImportStatus.Completed)
        {
            if (import.DerivedFromImportId.HasValue &&
                job.Status is JobExecutionStatus.Queued or JobExecutionStatus.Processing or JobExecutionStatus.Retrying)
            {
                job.Status = JobExecutionStatus.Completed;
                job.ProgressPercent = 100;
                job.ProgressMessage = "Arquivo processado";
                job.ResultJson ??= JsonSerializer.Serialize(new { importId, completed = true });
                job.FinishedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }
        if (import.Status == RouteImportStatus.Failed)
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
        job.ProgressMessage = "Processando arquivo";
        import.Status = RouteImportStatus.Processing;
        import.StartedAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var processor = processors.SingleOrDefault(x =>
            string.Equals(x.SourceCode, import.DataSource!.ProcessorKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new StructuralImportException(
                $"Não existe processador para a chave '{import.DataSource!.ProcessorKey}'.");

        try
        {
            await processor.ProcessAsync(import.Id, cancellationToken);
            dbContext.ChangeTracker.Clear();
            job = await dbContext.JobExecutions.SingleAsync(
                x => x.Id == jobExecutionId, cancellationToken);
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
            job.ProgressMessage = "Arquivo processado";
            job.ResultJson = JsonSerializer.Serialize(new { importId, completed = true });
            job.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            var activated = await importLifecycle.TryActivateAsync(import.Id, cancellationToken);
            if (!activated && import.DerivedFromImportId.HasValue)
            {
                activated = await dbContext.DataSources.AsNoTracking()
                    .AnyAsync(source => source.Id == import.DataSourceId && source.CurrentImportId == import.Id,
                        cancellationToken);
            }
            if (activated && (
                string.Equals(import.DataSource!.Code, RouteImportCodes.DataSource, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(import.DataSource.Code, CustomerImportCodes.DataSource, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(import.DataSource.Code, CustomerRouteAssignmentImportCodes.DataSource, StringComparison.OrdinalIgnoreCase)))
            {
                await routeCustomerAssignments.SyncInferredAssignmentsAsync(cancellationToken);
            }

            if (activated && string.Equals(import.DataSource!.Code, CustomerImportCodes.DataSource,
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await operationalJobQueue.TryQueueAsync(
                        OperationalJobCodes.MunicipalityCoordinateEnrichment,
                        import.Id,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // O enriquecimento pode ser reenfileirado manualmente pela Central de Processamentos.
                }
            }

            if (activated && import.DerivedFromImportId.HasValue &&
                string.Equals(import.DataSource!.Code, RouteImportCodes.DataSource, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var affectedWeekdays = await dbContext.RouteImportAffectedWeekdays.AsNoTracking()
                        .Where(item => item.ImportId == import.Id)
                        .OrderBy(item => item.Weekday)
                        .Select(item => item.Weekday)
                        .ToArrayAsync(cancellationToken);
                    await operationalJobQueue.TryQueueWithContextAsync(
                        OperationalJobCodes.DailyRouteOptimization,
                        import.Id,
                        JsonSerializer.Serialize(new { weekdays = affectedWeekdays }),
                        import.CreatedByUserId,
                        jobExecutionId,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // A versão já foi publicada. O job falho permanece na Central e pode ser repetido pela remediação.
                }
            }

            if (activated && string.Equals(import.DataSource!.Code, RouteImportCodes.DataSource,
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await operationalJobQueue.TryQueueAsync(
                        OperationalJobCodes.RouteCostConsolidation,
                        import.Id,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // A consolidação pode ser reenfileirada pela Central de Processamentos.
                }
            }

        }
        catch (StructuralImportException exception)
        {
            (job, import) = await ReloadStateAsync(importId, jobExecutionId, cancellationToken);
            import.Status = RouteImportStatus.Failed;
            import.FailureMessage = exception.Message;
            import.FinishedAt = DateTime.UtcNow;
            job.Status = JobExecutionStatus.Completed;
            job.ProgressPercent = 100;
            job.ProgressMessage = "Arquivo validado com inconsistências";
            job.ResultJson = JsonSerializer.Serialize(new { importId, needsReview = true });
            job.FinishedAt = DateTime.UtcNow;
            job.ErrorMessage = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            (job, import) = await ReloadStateAsync(importId, jobExecutionId, cancellationToken);
            job.ErrorMessage = LimitErrorMessage(exception.Message);
            if (job.Attempts >= MaximumAttempts)
            {
                job.Status = JobExecutionStatus.Failed;
                job.ProgressMessage = "Falha";
                job.FinishedAt = DateTime.UtcNow;
                var derivedWasPublished = import.DerivedFromImportId.HasValue &&
                    await dbContext.DataSources.AsNoTracking().AnyAsync(source =>
                        source.Id == import.DataSourceId && source.CurrentImportId == import.Id, cancellationToken);
                if (!derivedWasPublished)
                {
                    import.Status = RouteImportStatus.Failed;
                    import.FailureMessage = job.ErrorMessage;
                    import.FinishedAt = DateTime.UtcNow;
                }
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            job.Status = JobExecutionStatus.Retrying;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static string LimitErrorMessage(string message) =>
        message.Length <= MaximumPersistedErrorMessageLength
            ? message
            : message[..MaximumPersistedErrorMessageLength];

    private async Task<(InovaSkill.Importer.Domain.Entities.JobExecution Job,
        InovaSkill.Importer.Domain.Entities.RouteImport Import)> ReloadStateAsync(
        Guid importId,
        Guid jobExecutionId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var job = await dbContext.JobExecutions.SingleAsync(
            x => x.Id == jobExecutionId, cancellationToken);
        var import = await dbContext.RouteImports.SingleAsync(
            x => x.Id == importId, cancellationToken);
        return (job, import);
    }
}
