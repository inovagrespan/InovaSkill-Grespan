using EFCore.BulkExtensions;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Events;
using InovaSkill.Importer.Application.Validation;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Processing.Buffers;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class FileImportPipelineProcessor(
    ImportDbContext dbContext,
    IProcessingEventPublisher eventPublisher,
    IFileJobProgressNotifier fileJobProgressNotifier,
    IFileParserFactory fileParserFactory,
    IImportPreProcessingPipeline importPreProcessingPipeline,
    IFileSchemaProvider fileSchemaProvider,
    IRowValidator rowValidator,
    IConfiguration configuration,
    ILogger<FileImportPipelineProcessor> logger) : IFileImportPipelineProcessor
{
    private const int BatchSize = 5000;
    private static readonly TimeSpan StaleProcessingTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ProcessJobAsync(long jobId, CancellationToken cancellationToken)
    {
        await RecoverStaleJobsAsync(cancellationToken);
        var job = await dbContext.FileJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Job {JobId} was not found.", jobId);
            return;
        }

        if (job.Status is not (FileJobStatus.WaitingProcessing or FileJobStatus.ReadyToImport))
        {
            logger.LogInformation("Skipping job {JobId} with status {Status}.", job.Id, job.Status);
            return;
        }

        var nextStatus = job.StartNextStage();
        await SaveJobStateAsync(job.Id, cancellationToken);

        try
        {
            if (nextStatus == FileJobStatus.PreProcessing)
            {
                await PreProcessAndValidateAsync(job, cancellationToken);
                if (job.Status == FileJobStatus.ReadyToImport)
                {
                    await eventPublisher.PublishAsync(
                        ProcessingEventEnvelope.Create(ProcessingEventTypes.ImportRequested, job.Id),
                        cancellationToken);
                }
            }
            else
            {
                await ImportValidatedFileAsync(job, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            job.MarkFailed(BuildFailureReason(ex));
            AddJobLog(job.Id, CurrentStageFromStatus(job.Status), "Error", job.CurrentStep);
            await SaveJobStateAsync(job.Id, cancellationToken);
            logger.LogError(ex, "Failed processing job {JobId}. File: {FilePath}", job.Id, job.FilePath);
        }
    }

    private async Task PreProcessAndValidateAsync(FileJob job, CancellationToken cancellationToken)
    {
        var preProcessingStep = await StartStepAsync(job.Id, ImportProcessingStages.PreProcessing, "Pre-processamento iniciado.", cancellationToken);
        var resolvedSourcePath = ResolveExistingSourcePath(job.FilePath, configuration);
        if (!string.Equals(resolvedSourcePath, job.FilePath, StringComparison.Ordinal))
        {
            job.FilePath = resolvedSourcePath;
            await SaveJobStateAsync(job.Id, cancellationToken);
        }

        var existingErrors = await dbContext.ImportErrors
            .Where(x => x.FileJobId == job.Id)
            .ToListAsync(cancellationToken);

        if (existingErrors.Count > 0)
        {
            dbContext.ImportErrors.RemoveRange(existingErrors);
            await SaveJobStateAsync(job.Id, cancellationToken);
        }

        job.NormalizedFilePath = BuildNormalizedFilePath(job);
        EnsureParentDirectory(job.NormalizedFilePath);

        var parser = fileParserFactory.Create(job.FilePath);
        job.UpdateProgress("Lendo estrutura do arquivo", ImportStageProgress.CalculatePreProcessingCountingPercent(1), 0);
        await SaveJobStateAsync(job.Id, cancellationToken);

        var totalRows = await CountRowsAsync(job, parser, job.FilePath, cancellationToken);
        job.TotalRows = totalRows;
        job.ProcessedRows = 0;
        if (totalRows > 0)
        {
            job.UpdateProgress("Normalizando linhas do arquivo", ImportStageProgress.PreProcessingCountingMaxPercent, 0);
        }

        await SaveJobStateAsync(job.Id, cancellationToken);

        var errors = new List<ImportError>();
        var processedRows = 0;

        await using (var stream = new FileStream(job.NormalizedFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var writer = new StreamWriter(stream))
        {
            var tableRows = ReadTableRowsAsync(parser, job.FilePath, cancellationToken);
            var request = new ImportPreProcessingRequest(Path.GetFileName(job.FilePath), job.ImportFileTypeCode, tableRows);

            await foreach (var result in importPreProcessingPipeline.ProcessRowsAsync(request, cancellationToken))
            {
                processedRows++;

                if (!string.IsNullOrWhiteSpace(result.DetectedFileTypeCode))
                {
                    job.ImportFileTypeCode = result.DetectedFileTypeCode;
                }

                foreach (var rowError in result.Errors)
                {
                    errors.Add(BuildImportError(
                        job.Id,
                        rowError.RowNumber,
                        ImportProcessingStages.PreProcessing,
                        rowError.Column,
                        rowError.Message,
                        result.Row.Values,
                        result.DetectedFileTypeCode));
                }

                if (result.ShouldStopProcessing)
                {
                    break;
                }

                await WriteNormalizedRowAsync(writer, result.Row, cancellationToken);

                if (ShouldUpdatePreProcessingProgress(job, processedRows, totalRows))
                {
                    await UpdatePreProcessingProgressAsync(job, "Normalizando linhas do arquivo", processedRows, totalRows, cancellationToken);
                }
            }
        }

        totalRows = processedRows;
        job.TotalRows = processedRows;
        job.ProcessedRows = processedRows;
        job.UpdateProgress("Pre-processamento concluido", ImportStageProgress.CompletePercent, processedRows);
        FinishStep(preProcessingStep, "completed", processedRows, errors.Count);
        AddJobLog(job.Id, ImportProcessingStages.PreProcessing, "Information", "Pre-processamento concluido.");
        await SaveJobStateAsync(job.Id, cancellationToken);

        job.MarkValidating();
        await SaveJobStateAsync(job.Id, cancellationToken);

        await ValidateNormalizedFileAsync(job, errors, cancellationToken);
    }

    private async Task ValidateNormalizedFileAsync(FileJob job, List<ImportError> errors, CancellationToken cancellationToken)
    {
        var validationStep = await StartStepAsync(job.Id, ImportProcessingStages.Validation, "Validacao iniciada.", cancellationToken);
        var processedRows = 0;

        await foreach (var normalizedRow in ReadNormalizedRowsAsync(job.NormalizedFilePath, cancellationToken))
        {
            processedRows++;

            var validationErrors = ValidateRow(normalizedRow, job.ImportFileTypeCode ?? string.Empty);
            foreach (var verr in validationErrors)
            {
                errors.Add(BuildImportError(
                    job.Id,
                    normalizedRow.RowNumber,
                    ImportProcessingStages.Validation,
                    verr.Column,
                    verr.Message,
                    normalizedRow.Values,
                    job.ImportFileTypeCode));
            }


            if (ShouldUpdateProgress(job, processedRows, job.TotalRows))
            {
                await UpdateProgressAsync(job, "Validando arquivo normalizado", processedRows, job.TotalRows, cancellationToken);
            }
        }

        if (job.TotalRows == 0)
        {
            errors.Add(new ImportError
            {
                FileJobId = job.Id,
                RowNumber = 0,
                Stage = ImportProcessingStages.Validation,
                Column = "File",
                Message = "File has no data rows.",
                RecordIdentifier = string.Empty
            });
        }

        if (errors.Count > 0)
        {
            await dbContext.BulkInsertAsync(errors, cancellationToken: cancellationToken);
            job.MarkValidationFailed();
            FinishStep(validationStep, "failed", processedRows, errors.Count);
            AddJobLog(job.Id, ImportProcessingStages.Validation, "Warning", $"Validacao concluida com {errors.Count} erro(s).");
        }
        else
        {
            job.MarkReadyToImport();
            FinishStep(validationStep, "completed", processedRows, 0);
            AddJobLog(job.Id, ImportProcessingStages.Validation, "Information", "Validacao concluida sem erros.");
        }

        job.ProcessedRows = job.TotalRows;
        await SaveJobStateAsync(job.Id, cancellationToken);
    }

    private async Task ImportValidatedFileAsync(FileJob job, CancellationToken cancellationToken)
    {
        var importStep = await StartStepAsync(job.Id, ImportProcessingStages.Import, "Importacao iniciada.", cancellationToken);
        if (string.IsNullOrWhiteSpace(job.ImportFileTypeCode))
        {
            job.MarkFailed("Tipo de arquivo nao identificado para importacao.");
            FinishStep(importStep, "failed", job.ProcessedRows, 1);
            AddJobLog(job.Id, ImportProcessingStages.Import, "Error", job.CurrentStep);
            await SaveJobStateAsync(job.Id, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(job.NormalizedFilePath) || !File.Exists(job.NormalizedFilePath))
        {
            job.MarkFailed("Arquivo normalizado nao encontrado para importacao.");
            FinishStep(importStep, "failed", job.ProcessedRows, 1);
            AddJobLog(job.Id, ImportProcessingStages.Import, "Error", job.CurrentStep);
            await SaveJobStateAsync(job.Id, cancellationToken);
            logger.LogError("Normalized file not found for job {JobId}: {Path}", job.Id, job.NormalizedFilePath);
            return;
        }

        var importValidationErrors = await ValidateRowsBeforeImportAsync(job, cancellationToken);
        if (importValidationErrors.Count > 0)
        {
            await dbContext.BulkInsertAsync(importValidationErrors, cancellationToken: cancellationToken);
            job.MarkValidationFailed();
            FinishStep(importStep, "failed", 0, importValidationErrors.Count);
            AddJobLog(job.Id, ImportProcessingStages.Import, "Warning", $"Importacao bloqueada por {importValidationErrors.Count} erro(s) de validacao.");
            await SaveJobStateAsync(job.Id, cancellationToken);
            return;
        }

        var buffer = CreateBuffer(job.ImportFileTypeCode ?? string.Empty);
        var processedRows = 0;

        await foreach (var row in ReadNormalizedRowsAsync(job.NormalizedFilePath, cancellationToken))
        {
            processedRows++;

            buffer.Add(row, job.Id);
            if (buffer.Count >= BatchSize)
            {
                await buffer.FlushAsync(dbContext, cancellationToken);
            }

            if (ShouldUpdateProgress(job, processedRows, job.TotalRows))
            {
                await UpdateProgressAsync(job, "Importando dados", processedRows, job.TotalRows, cancellationToken);
            }
        }

        if (buffer.Count > 0)
        {
            await buffer.FlushAsync(dbContext, cancellationToken);
        }

        job.MarkCompleted(job.TotalRows > 0 ? job.TotalRows : processedRows);
        FinishStep(importStep, "completed", processedRows, 0);
        AddJobLog(job.Id, ImportProcessingStages.Import, "Information", "Importacao concluida.");
        await SaveJobStateAsync(job.Id, cancellationToken);

        if (string.Equals(job.ImportFileTypeCode, ImportFileTypeCodes.SalesInvoice, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await eventPublisher.PublishAsync(
                    ProcessingEventEnvelope.Create(ProcessingEventTypes.SummaryGenerationRequested, job.Id, BuildSummaryPayload(PostImportJobType.SalesSummary)),
                    cancellationToken);
                await eventPublisher.PublishAsync(
                    ProcessingEventEnvelope.Create(ProcessingEventTypes.SummaryGenerationRequested, job.Id, BuildSummaryPayload(PostImportJobType.CustomerSummary)),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enqueue post-import summary jobs for file job {JobId}.", job.Id);
            }
        }

        // TryDeleteNormalizedFile(job.NormalizedFilePath);
    }

    private async Task UpdateProgressAsync(
        FileJob job,
        string step,
        int processedRows,
        int totalRows,
        CancellationToken cancellationToken)
    {
        var progressPercent = ImportStageProgress.CalculatePercent(processedRows, totalRows);

        job.UpdateProgress(step, progressPercent, processedRows);
        await SaveJobStateAsync(job.Id, cancellationToken);
    }

    private async Task<ProcessingStepExecution> StartStepAsync(long fileJobId, string step, string message, CancellationToken cancellationToken)
    {
        var execution = new ProcessingStepExecution
        {
            FileJobId = fileJobId,
            Step = step,
            Status = "running",
            StartedAt = DateTime.UtcNow
        };

        dbContext.ProcessingStepExecutions.Add(execution);
        AddJobLog(fileJobId, step, "Information", message);
        await dbContext.SaveChangesAsync(cancellationToken);
        return execution;
    }

    private static void FinishStep(ProcessingStepExecution execution, string status, int processedRows, int errorCount)
    {
        execution.Status = status;
        execution.FinishedAt = DateTime.UtcNow;
        execution.ProcessedRows = processedRows;
        execution.ErrorCount = errorCount;
    }

    private void AddJobLog(long fileJobId, string stage, string level, string message)
    {
        dbContext.ProcessingJobLogs.Add(new ProcessingJobLog
        {
            FileJobId = fileJobId,
            Stage = stage,
            Level = level,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task UpdatePreProcessingProgressAsync(
        FileJob job,
        string step,
        int processedRows,
        int totalRows,
        CancellationToken cancellationToken)
    {
        var progressPercent = ImportStageProgress.CalculatePreProcessingNormalizationPercent(processedRows, totalRows);

        job.UpdateProgress(step, progressPercent, processedRows);
        await SaveJobStateAsync(job.Id, cancellationToken);
    }

    internal async Task<List<ImportError>> ValidateRowsBeforeImportAsync(FileJob job, CancellationToken cancellationToken)
    {
        var errors = new List<ImportError>();

        await foreach (var row in ReadNormalizedRowsAsync(job.NormalizedFilePath, cancellationToken))
        {
            var validationErrors = ValidateRow(row, job.ImportFileTypeCode ?? string.Empty);
            foreach (var validationError in validationErrors)
            {
                errors.Add(BuildImportError(
                    job.Id,
                    row.RowNumber,
                    ImportProcessingStages.Import,
                    validationError.Column,
                    validationError.Message,
                    row.Values,
                    job.ImportFileTypeCode));
            }
        }

        return errors;
    }

    private static bool ShouldUpdateProgress(FileJob job, int processedRows, int totalRows)
    {
        var currentPercent = ImportStageProgress.CalculatePercent(processedRows, totalRows);
        return ImportStageProgress.ShouldUpdate(processedRows, totalRows, currentPercent, job.ProgressPercent);
    }

    private static bool ShouldUpdatePreProcessingProgress(FileJob job, int processedRows, int totalRows)
    {
        var currentPercent = ImportStageProgress.CalculatePreProcessingNormalizationPercent(processedRows, totalRows);
        return ImportStageProgress.ShouldUpdate(processedRows, totalRows, currentPercent, job.ProgressPercent);
    }

    private static async IAsyncEnumerable<TableRow> ReadTableRowsAsync(
        IFileParser parser,
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in parser.ParseAsync(filePath, cancellationToken))
        {
            yield return new TableRow(row.RowNumber, row.Values);
        }
    }

    private async Task<int> CountRowsAsync(
        FileJob job,
        IFileParser parser,
        string filePath,
        CancellationToken cancellationToken)
    {
        var total = 0;
        await foreach (var _ in parser.ParseAsync(filePath, cancellationToken))
        {
            total++;

            var currentPercent = ImportStageProgress.CalculatePreProcessingCountingPercent(total);
            if (ImportStageProgress.ShouldUpdateCounting(total, job.ProcessedRows, currentPercent, job.ProgressPercent))
            {
                job.TotalRows = total;
                job.UpdateProgress("Contando linhas do arquivo", currentPercent, total);
                await SaveJobStateAsync(job.Id, cancellationToken);
            }
        }

        return total;
    }

    private async Task RecoverStaleJobsAsync(CancellationToken cancellationToken)
    {
        var staleAt = DateTime.UtcNow - StaleProcessingTimeout;

        var staleJobs = await dbContext.FileJobs
            .Where(x =>
                (x.Status == FileJobStatus.PreProcessing || x.Status == FileJobStatus.Validating || x.Status == FileJobStatus.Importing) &&
                x.LastHeartbeatAt < staleAt)
            .OrderBy(x => x.LastHeartbeatAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (staleJobs.Count == 0)
        {
            return;
        }

        foreach (var job in staleJobs)
        {
            if (job.Status == FileJobStatus.Importing)
            {
                await CleanupImportedDataByJobAsync(job, cancellationToken);
                job.RecoverAfterStaleProcessing();
            }
            else
            {
                // TryDeleteNormalizedFile(job.NormalizedFilePath);
                // job.NormalizedFilePath = string.Empty;
                job.RecoverAfterStaleValidation();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var job in staleJobs)
        {
            await fileJobProgressNotifier.NotifyAsync(job.Id, cancellationToken);
        }
    }

    private async Task SaveJobStateAsync(long jobId, CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        await fileJobProgressNotifier.NotifyAsync(jobId, cancellationToken);
    }

    private async Task CleanupImportedDataByJobAsync(FileJob job, CancellationToken cancellationToken)
    {
        if (string.Equals(job.ImportFileTypeCode, ImportFileTypeCodes.CustomerList, StringComparison.OrdinalIgnoreCase))
        {
            await dbContext.Customers.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            return;
        }

        if (string.Equals(job.ImportFileTypeCode, ImportFileTypeCodes.ProductList, StringComparison.OrdinalIgnoreCase))
        {
            await dbContext.Products.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            return;
        }

        if (string.Equals(job.ImportFileTypeCode, ImportFileTypeCodes.FinancialEntry, StringComparison.OrdinalIgnoreCase))
        {
            await dbContext.Orders.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            return;
        }

        if (string.Equals(job.ImportFileTypeCode, ImportFileTypeCodes.SalesInvoice, StringComparison.OrdinalIgnoreCase))
        {
            await dbContext.CommercialTransactions.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static string ExtractIdentifier(IReadOnlyDictionary<string, string> values, string? importFileTypeCode)
    {
        return importFileTypeCode?.ToUpperInvariant() switch
        {
            ImportFileTypeCodes.CustomerList => TryValue(values, "email"),
            ImportFileTypeCodes.ProductList => TryValue(values, "sku"),
            ImportFileTypeCodes.FinancialEntry => TryValue(values, "ordernumber"),
            ImportFileTypeCodes.SalesInvoice => TryValue(values, "documentnumber"),
            _ => string.Empty
        };
    }

    private static string TryValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static IFileTypeBuffer CreateBuffer(string importFileTypeCode)
    {
        return importFileTypeCode?.ToUpperInvariant() switch
        {
            ImportFileTypeCodes.CustomerList => new CustomerBuffer(),
            ImportFileTypeCodes.ProductList => new ProductBuffer(),
            ImportFileTypeCodes.FinancialEntry => new OrderBuffer(),
            ImportFileTypeCodes.SalesInvoice => new CommercialTransactionBuffer(),
            _ => throw new InvalidOperationException($"Unsupported file type code: {importFileTypeCode}")
        };
    }

    private IReadOnlyList<ValidationError> ValidateRow(ImportedRow row, string detectedFileTypeCode)
    {
        var schema = fileSchemaProvider.GetSchema(detectedFileTypeCode);
        var validation = rowValidator.Validate(row, schema);
        return validation.Errors;
    }

    private static string CurrentStageFromStatus(FileJobStatus status)
    {
        return status switch
        {
            FileJobStatus.PreProcessing => ImportProcessingStages.PreProcessing,
            FileJobStatus.Validating or FileJobStatus.ValidationFailed => ImportProcessingStages.Validation,
            FileJobStatus.Importing or FileJobStatus.Completed => ImportProcessingStages.Import,
            _ => "PROCESSING"
        };
    }

    private static ImportError BuildImportError(
        long fileJobId,
        int rowNumber,
        string stage,
        string column,
        string message,
        IReadOnlyDictionary<string, string> values,
        string? detectedFileTypeCode)
    {
        return new ImportError
        {
            FileJobId = fileJobId,
            RowNumber = rowNumber,
            Stage = stage,
            Column = column,
            Message = message,
            RecordIdentifier = ExtractIdentifier(values, detectedFileTypeCode)
        };
    }

    private static string BuildNormalizedFilePath(FileJob job)
    {
        var directory = Path.GetDirectoryName(job.FilePath) ?? AppContext.BaseDirectory;
        var fileName = $"{Path.GetFileNameWithoutExtension(job.FilePath)}.{job.Id}.normalized.ndjson";
        return Path.Combine(directory, fileName);
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static async Task WriteNormalizedRowAsync(StreamWriter writer, ImportedRow row, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new NormalizedRow(row.RowNumber, new Dictionary<string, string>(row.Values)), JsonOptions);
        await writer.WriteLineAsync(payload.AsMemory(), cancellationToken);
    }

    private static JsonElement BuildSummaryPayload(PostImportJobType jobType)
    {
        return JsonSerializer.SerializeToElement(new { jobType = jobType.ToString() }, JsonOptions);
    }

    private static async IAsyncEnumerable<ImportedRow> ReadNormalizedRowsAsync(string normalizedFilePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(normalizedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parsed = JsonSerializer.Deserialize<NormalizedRow>(line, JsonOptions);
            if (parsed is null)
            {
                continue;
            }

            yield return new ImportedRow(parsed.RowNumber, parsed.Values ?? new Dictionary<string, string>());
        }
    }

    // private static void TryDeleteNormalizedFile(string path)
    // {
    //     try
    //     {
    //         if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
    //         {
    //             File.Delete(path);
    //         }
    //     }
    //     catch
    //     {
    //         // best effort cleanup only
    //     }
    // }

    private sealed record NormalizedRow(int RowNumber, Dictionary<string, string> Values);




    private static string BuildFailureReason(Exception ex)
    {
        var message = ex.Message.Trim();

        if (ex is InvalidOperationException)
        {
            if (message.Contains("Unsupported extension", StringComparison.OrdinalIgnoreCase))
            {
                return "Extensao de arquivo nao suportada. Use CSV ou XLSX.";
            }

            if (message.Contains("Unsupported file type", StringComparison.OrdinalIgnoreCase))
            {
                return "Tipo de arquivo nao suportado para importacao.";
            }

            if (message.Contains("Unsupported rule type", StringComparison.OrdinalIgnoreCase))
            {
                return "Regra de pre-processamento nao suportada.";
            }

            return $"Erro de configuracao: {message}";
        }

        if (ex is JsonException)
        {
            return "Falha ao ler dados normalizados durante o processamento.";
        }

        if (ex is IOException)
        {
            return "Falha de acesso ao arquivo durante o processamento.";
        }

        var postgresException = FindException<PostgresException>(ex);
        if (postgresException?.SqlState == PostgresErrorCodes.NumericValueOutOfRange ||
            ContainsNumericOverflowSignal(ex))
        {
            return "Valor numerico excede o limite permitido pelo banco de dados.";
        }

        return $"Erro inesperado: {message}";
    }

    private static TException? FindException<TException>(Exception exception)
        where TException : Exception
    {
        var current = exception;
        while (current is not null)
        {
            if (current is TException matched)
            {
                return matched;
            }

            current = current.InnerException;
        }

        return null;
    }

    private static bool ContainsNumericOverflowSignal(Exception exception)
    {
        var details = exception.ToString();
        return details.Contains(PostgresErrorCodes.NumericValueOutOfRange, StringComparison.OrdinalIgnoreCase) ||
            details.Contains("numeric field overflow", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveExistingSourcePath(string currentPath, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(currentPath) && File.Exists(currentPath))
        {
            return currentPath;
        }

        var fileName = ExtractFileNameCrossPlatform(currentPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return currentPath;
        }

        var configuredUploadsPath = configuration["Storage:UploadsPath"];
        if (!string.IsNullOrWhiteSpace(configuredUploadsPath))
        {
            var candidate = Path.Combine(configuredUploadsPath, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads");
        var fallback = Path.Combine(uploadsDir, fileName);
        return File.Exists(fallback) ? fallback : currentPath;
    }

    private static string ExtractFileNameCrossPlatform(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/');
        var idx = normalized.LastIndexOf('/');
        return idx >= 0 ? normalized[(idx + 1)..] : normalized;
    }

}





