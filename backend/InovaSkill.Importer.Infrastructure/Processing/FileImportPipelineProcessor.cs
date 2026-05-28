using EFCore.BulkExtensions;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Validation;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Processing.Buffers;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class FileImportPipelineProcessor(
    ImportDbContext dbContext,
    IFileJobQueue fileJobQueue,
    IFileParserFactory fileParserFactory,
    IPreProcessorTemplateResolver preProcessorTemplateResolver,
    IPreProcessorRuleEngine preProcessorRuleEngine,
    IFileTypeDetector fileTypeDetector,
    IFileSchemaProvider fileSchemaProvider,
    IRowValidator rowValidator,
    ILogger<FileImportPipelineProcessor> logger) : IFileImportPipelineProcessor
{
    private const int BatchSize = 5000;
    private const int ProgressUpdateIntervalRows = 200;
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
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            if (nextStatus == FileJobStatus.PreProcessing)
            {
                await PreProcessAndValidateAsync(job, cancellationToken);
                if (job.Status == FileJobStatus.ReadyToImport)
                {
                    await fileJobQueue.EnqueueAsync(job.Id, cancellationToken);
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
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "Failed processing job {JobId}. File: {FilePath}", job.Id, job.FilePath);
        }
    }

    private async Task PreProcessAndValidateAsync(FileJob job, CancellationToken cancellationToken)
    {
        var existingErrors = await dbContext.ImportErrors
            .Where(x => x.FileJobId == job.Id)
            .ToListAsync(cancellationToken);

        if (existingErrors.Count > 0)
        {
            dbContext.ImportErrors.RemoveRange(existingErrors);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        job.NormalizedFilePath = BuildNormalizedFilePath(job);
        EnsureParentDirectory(job.NormalizedFilePath);

        var parser = fileParserFactory.Create(job.FilePath);
        var parserForCounting = fileParserFactory.Create(job.FilePath);
        var totalRows = await CountRowsAsync(parserForCounting, job.FilePath, cancellationToken);
        job.TotalRows = totalRows;
        await dbContext.SaveChangesAsync(cancellationToken);

        var errors = new List<ImportError>();
        var detectedFileType = job.FileType;
        var detectionAttempted = detectedFileType != FileType.Unknown;
        var processedRows = 0;
        PreProcessorTemplate? template = null;
        IReadOnlyList<PreProcessorTemplateRule>? transformRules = null;
        IReadOnlyList<PreProcessorTemplateRule>? validationRules = null;

        await using (var stream = new FileStream(job.NormalizedFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var writer = new StreamWriter(stream))
        {
            await foreach (var row in parser.ParseAsync(job.FilePath, cancellationToken))
            {
                processedRows++;

                if (processedRows == 1)
                {
                    template = await preProcessorTemplateResolver.ResolveAsync(
                        Path.GetFileName(job.FilePath),
                        row.Values.Keys.ToArray(),
                        cancellationToken);

                    if (template is not null)
                    {
                        var ordered = template.Rules.OrderBy(x => x.SortOrder).ToList();
                        transformRules = ordered.Where(x => !IsValidationRule(x)).ToList();
                        validationRules = ordered.Where(IsValidationRule).ToList();
                        if (template.FileType != FileType.Unknown && job.FileType == FileType.Unknown)
                        {
                            job.FileType = template.FileType;
                        }
                    }
                }

                var preprocessResult = PreprocessRow(row, transformRules);
                var normalizedRow = ApplyBuiltInAliases(preprocessResult.Row);

                foreach (var ruleError in preprocessResult.Errors)
                {
                    errors.Add(BuildImportError(job.Id, normalizedRow.RowNumber, ruleError.Column, ruleError.Message, normalizedRow.Values, detectedFileType));
                }

                if (!detectionAttempted)
                {
                    detectedFileType = fileTypeDetector.Detect(normalizedRow.Values);
                    detectionAttempted = true;
                    job.FileType = detectedFileType;

                    if (detectedFileType == FileType.Unknown)
                    {
                        errors.Add(new ImportError
                        {
                            FileJobId = job.Id,
                            RowNumber = normalizedRow.RowNumber,
                            Column = "FileType",
                            Message = "Unable to detect file type from header.",
                            RecordIdentifier = string.Empty
                        });
                        break;
                    }
                }

                await WriteNormalizedRowAsync(writer, normalizedRow, cancellationToken);

                var shouldUpdateProgress = processedRows % ProgressUpdateIntervalRows == 0;
                if (shouldUpdateProgress)
                {
                    await UpdateProgressAsync(job, "Pre-processando arquivo", 0, 30, processedRows, totalRows, cancellationToken);
                }
            }
        }

        job.TotalRows = totalRows;
        job.ProcessedRows = processedRows;
        job.UpdateProgress("Pre-processamento concluido", 30, processedRows);
        await dbContext.SaveChangesAsync(cancellationToken);

        job.MarkValidating();
        await dbContext.SaveChangesAsync(cancellationToken);

        await ValidateNormalizedFileAsync(job, validationRules, errors, cancellationToken);
    }

    private async Task ValidateNormalizedFileAsync(FileJob job, IReadOnlyList<PreProcessorTemplateRule>? validationRules, List<ImportError> errors, CancellationToken cancellationToken)
    {
        var processedRows = 0;

        await foreach (var normalizedRow in ReadNormalizedRowsAsync(job.NormalizedFilePath, cancellationToken))
        {
            processedRows++;

            var validationErrors = ValidateRow(normalizedRow, job.FileType);
            foreach (var verr in validationErrors)
            {
                errors.Add(BuildImportError(job.Id, normalizedRow.RowNumber, verr.Column, verr.Message, normalizedRow.Values, job.FileType));
            }

            if (validationRules is { Count: > 0 })
            {
                var ruleValidation = preProcessorRuleEngine.Execute(normalizedRow, validationRules);
                foreach (var err in ruleValidation.Errors)
                {
                    errors.Add(BuildImportError(job.Id, normalizedRow.RowNumber, err.Column, err.Message, normalizedRow.Values, job.FileType));
                }
            }

            var shouldUpdateProgress = processedRows % ProgressUpdateIntervalRows == 0;
            if (shouldUpdateProgress)
            {
                await UpdateProgressAsync(job, "Validando arquivo normalizado", 30, 60, processedRows, job.TotalRows, cancellationToken);
            }
        }

        if (job.TotalRows == 0)
        {
            errors.Add(new ImportError
            {
                FileJobId = job.Id,
                RowNumber = 0,
                Column = "File",
                Message = "File has no data rows.",
                RecordIdentifier = string.Empty
            });
        }

        if (errors.Count > 0)
        {
            await dbContext.BulkInsertAsync(errors, cancellationToken: cancellationToken);
            job.MarkValidationFailed();
        }
        else
        {
            job.MarkReadyToImport();
        }

        job.ProcessedRows = job.TotalRows;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ImportValidatedFileAsync(FileJob job, CancellationToken cancellationToken)
    {
        if (job.FileType == FileType.Unknown)
        {
            job.MarkFailed("Tipo de arquivo nao identificado para importacao.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(job.NormalizedFilePath) || !File.Exists(job.NormalizedFilePath))
        {
            job.MarkFailed("Arquivo normalizado nao encontrado para importacao.");
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogError("Normalized file not found for job {JobId}: {Path}", job.Id, job.NormalizedFilePath);
            return;
        }

        var buffer = CreateBuffer(job.FileType);
        var processedRows = 0;

        await foreach (var row in ReadNormalizedRowsAsync(job.NormalizedFilePath, cancellationToken))
        {
            processedRows++;

            buffer.Add(row, job.Id);
            if (buffer.Count >= BatchSize)
            {
                await buffer.FlushAsync(dbContext, cancellationToken);
            }

            var shouldUpdateProgress = processedRows % ProgressUpdateIntervalRows == 0;
            if (shouldUpdateProgress)
            {
                await UpdateProgressAsync(job, "Importando dados", 60, 100, processedRows, job.TotalRows, cancellationToken);
            }
        }

        if (buffer.Count > 0)
        {
            await buffer.FlushAsync(dbContext, cancellationToken);
        }

        job.MarkCompleted(job.TotalRows > 0 ? job.TotalRows : processedRows);
        await dbContext.SaveChangesAsync(cancellationToken);

        // TryDeleteNormalizedFile(job.NormalizedFilePath);
    }

    private async Task UpdateProgressAsync(
        FileJob job,
        string step,
        int startPercent,
        int endPercent,
        int processedRows,
        int totalRows,
        CancellationToken cancellationToken)
    {
        var progressPercent = startPercent;
        if (totalRows > 0)
        {
            var local = (double)processedRows / totalRows;
            var mapped = startPercent + ((endPercent - startPercent) * local);
            progressPercent = Math.Clamp((int)Math.Round(mapped), startPercent, endPercent);
        }

        job.UpdateProgress(step, progressPercent, processedRows);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<int> CountRowsAsync(IFileParser parser, string filePath, CancellationToken cancellationToken)
    {
        var total = 0;
        await foreach (var _ in parser.ParseAsync(filePath, cancellationToken))
        {
            total++;
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
    }

    private async Task CleanupImportedDataByJobAsync(FileJob job, CancellationToken cancellationToken)
    {
        if (job.FileType == FileType.Customers)
        {
            await dbContext.Customers.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            return;
        }

        if (job.FileType == FileType.Products)
        {
            await dbContext.Products.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            return;
        }

        if (job.FileType == FileType.Orders)
        {
            await dbContext.Orders.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static string ExtractIdentifier(IReadOnlyDictionary<string, string> values, FileType fileType)
    {
        return fileType switch
        {
            FileType.Customers => TryValue(values, "email"),
            FileType.Products => TryValue(values, "sku"),
            FileType.Orders => TryValue(values, "ordernumber"),
            _ => string.Empty
        };
    }

    private static string TryValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static IFileTypeBuffer CreateBuffer(FileType fileType)
    {
        return fileType switch
        {
            FileType.Customers => new CustomerBuffer(),
            FileType.Products => new ProductBuffer(),
            FileType.Orders => new OrderBuffer(),
            _ => throw new InvalidOperationException($"Unsupported file type: {fileType}")
        };
    }

    private PreProcessorExecutionResult PreprocessRow(ImportedRow row, IReadOnlyList<PreProcessorTemplateRule>? orderedRules)
    {
        if (orderedRules is null || orderedRules.Count == 0)
        {
            return new PreProcessorExecutionResult(row, []);
        }

        return preProcessorRuleEngine.Execute(row, orderedRules);
    }

    private IReadOnlyList<ValidationError> ValidateRow(ImportedRow row, FileType detectedFileType)
    {
        var schema = fileSchemaProvider.GetSchema(detectedFileType);
        var validation = rowValidator.Validate(row, schema);
        return validation.Errors;
    }

    private static ImportError BuildImportError(
        long fileJobId,
        int rowNumber,
        string column,
        string message,
        IReadOnlyDictionary<string, string> values,
        FileType detectedFileType)
    {
        return new ImportError
        {
            FileJobId = fileJobId,
            RowNumber = rowNumber,
            Column = column,
            Message = message,
            RecordIdentifier = ExtractIdentifier(values, detectedFileType)
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

    private static ImportedRow ApplyBuiltInAliases(ImportedRow row)
    {
        var values = new Dictionary<string, string>(row.Values, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        // Orders: legacy header alias support.
        if (!values.ContainsKey("orderedat") && values.TryGetValue("orderdate", out var orderDateValue))
        {
            values["orderedat"] = orderDateValue;
            changed = true;
        }

        return changed ? new ImportedRow(row.RowNumber, values) : row;
    }

    private static bool IsValidationRule(PreProcessorTemplateRule rule)
    {
        var type = rule.RuleType.Trim().ToLowerInvariant();
        return type.StartsWith("validate_", StringComparison.Ordinal);
    }

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

        return $"Erro inesperado: {message}";
    }
}
