using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController(
    IFileUploadService fileUploadService,
    IFileJobQueue fileJobQueue,
    ImportDbContext dbContext) : ControllerBase
{
    private static readonly HashSet<string> AllowedImportFileTypeCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ImportFileTypeCodes.SalesInvoice,
        ImportFileTypeCodes.CustomerList,
        ImportFileTypeCodes.ProductList,
        ImportFileTypeCodes.FinancialEntry
    };

    [HttpPost("upload")]
    [RequestSizeLimit(524_288_000)]
    public async Task<ActionResult<UploadResponse>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? importFileTypeCode = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Arquivo inválido",
                Detail = "Nenhum arquivo foi enviado. Selecione um arquivo CSV ou XLSX.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var normalizedImportFileTypeCode = string.IsNullOrWhiteSpace(importFileTypeCode)
            ? null
            : importFileTypeCode.Trim().ToUpperInvariant();

        if (normalizedImportFileTypeCode is not null && !AllowedImportFileTypeCodes.Contains(normalizedImportFileTypeCode))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Destino de importação inválido",
                Detail = "Selecione um destino válido para importação.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var fileJobId = await fileUploadService.UploadAndCreateJobAsync(
                stream,
                file.FileName,
                normalizedImportFileTypeCode,
                cancellationToken);
            return Ok(new UploadResponse(fileJobId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Não foi possível importar o arquivo",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno ao importar arquivo",
                Detail = $"Falha ao processar o upload de '{file.FileName}'. Tente novamente.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    [HttpPost("jobs/{jobId:long}/retry")]
    public async Task<ActionResult> RetryJob(long jobId, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.FileJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Arquivo não encontrado",
                Detail = $"Nenhum arquivo com ID {jobId} foi encontrado.",
                Status = StatusCodes.Status404NotFound
            });
        }

        if (job.Status == Domain.Enums.FileJobStatus.Importing)
        {
            if (string.Equals(job.ImportFileTypeCode, ImportFileTypeCodes.CustomerList, StringComparison.OrdinalIgnoreCase))
            {
                await dbContext.Customers.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            }
            else if (string.Equals(job.ImportFileTypeCode, ImportFileTypeCodes.ProductList, StringComparison.OrdinalIgnoreCase))
            {
                await dbContext.Products.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            }
            else if (string.Equals(job.ImportFileTypeCode, ImportFileTypeCodes.FinancialEntry, StringComparison.OrdinalIgnoreCase))
            {
                await dbContext.Orders.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            }
            else if (string.Equals(job.ImportFileTypeCode, ImportFileTypeCodes.SalesInvoice, StringComparison.OrdinalIgnoreCase))
            {
                await dbContext.CommercialTransactions.Where(x => x.SourceFileJobId == job.Id).ExecuteDeleteAsync(cancellationToken);
            }
        }

        if (job.Status == Domain.Enums.FileJobStatus.ValidationFailed)
        {
            job.ImportFileTypeCode = null;
        }

        job.RequeueManually();

        await dbContext.SaveChangesAsync(cancellationToken);
        await fileJobQueue.EnqueueAsync(job.Id, cancellationToken);
        return Ok();
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<PagedResult<FileJobDto>>> GetJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var baseQuery = dbContext.FileJobs.AsNoTracking();
        var total = await baseQuery.CountAsync(cancellationToken);

        var jobs = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var jobIds = jobs.Select(x => x.Id).ToArray();
        var errorCounts = await dbContext.ImportErrors
            .AsNoTracking()
            .Where(x => jobIds.Contains(x.FileJobId))
            .GroupBy(x => new { x.FileJobId, x.Stage })
            .Select(x => new
            {
                x.Key.FileJobId,
                x.Key.Stage,
                Count = x.Count()
            })
            .ToListAsync(cancellationToken);

        var errorCountLookup = errorCounts
            .GroupBy(x => (x.FileJobId, NormalizeStageCode(x.Stage)))
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Count));

        var items = jobs.Select(x =>
        {
            var stages = BuildStageProgress(x, errorCountLookup);
            var currentStage = stages.FirstOrDefault(stage => stage.Status == StageProgressStatus.Running);

            return new FileJobDto(
                x.Id,
                Path.GetFileName(x.FilePath),
                x.ImportFileTypeCode,
                x.Status,
                x.CreatedAt,
                stages.Sum(stage => stage.ErrorCount),
                x.CurrentStep,
                x.ProgressPercent,
                x.ProcessedRows,
                x.TotalRows,
                currentStage?.Code,
                currentStage?.Name,
                stages);
        }).ToList();

        return Ok(new PagedResult<FileJobDto>(page, pageSize, total, items));
    }

    [HttpGet("jobs/{jobId:long}/errors")]
    public async Task<ActionResult<PagedResult<ImportErrorDto>>> GetJobErrors(
        long jobId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 500);

        var exists = await dbContext.FileJobs.AnyAsync(x => x.Id == jobId, cancellationToken);
        if (!exists)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Arquivo não encontrado",
                Detail = $"Nenhum arquivo com ID {jobId} foi encontrado.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var baseQuery = dbContext.ImportErrors
            .AsNoTracking()
            .Where(x => x.FileJobId == jobId);

        var total = await baseQuery.CountAsync(cancellationToken);

        var errorRows = await dbContext.ImportErrors
            .AsNoTracking()
            .Where(x => x.FileJobId == jobId)
            .OrderBy(x => x.RowNumber)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var errors = errorRows
            .Select(x => new ImportErrorDto(
                x.Id,
                x.FileJobId,
                x.RowNumber,
                NormalizeStageCode(x.Stage),
                x.Column,
                x.Message,
                x.RecordIdentifier))
            .ToList();

        return Ok(new PagedResult<ImportErrorDto>(page, pageSize, total, errors));
    }

    private static IReadOnlyList<FileJobStageProgressDto> BuildStageProgress(
        FileJob job,
        IReadOnlyDictionary<(long FileJobId, string Stage), int> errorCountLookup)
    {
        return ImportProcessingStages.All
            .Select(stage => new FileJobStageProgressDto(
                stage.Code,
                stage.Name,
                ResolveStageStatus(job, stage.Code, GetStageErrorCount(job.Id, stage.Code, errorCountLookup)),
                ResolveStageProgress(job, stage.Code),
                GetStageErrorCount(job.Id, stage.Code, errorCountLookup)))
            .ToList();
    }

    private static int GetStageErrorCount(
        long fileJobId,
        string stage,
        IReadOnlyDictionary<(long FileJobId, string Stage), int> errorCountLookup)
    {
        return errorCountLookup.TryGetValue((fileJobId, stage), out var count) ? count : 0;
    }

    private static string ResolveStageStatus(FileJob job, string stage, int errorCount)
    {
        if (job.Status == FileJobStatus.Failed && IsCurrentFailureStage(job, stage))
        {
            return StageProgressStatus.Failed;
        }

        return stage switch
        {
            ImportProcessingStages.PreProcessing => ResolvePreProcessingStatus(job, errorCount),
            ImportProcessingStages.Validation => ResolveValidationStatus(job, errorCount),
            ImportProcessingStages.Import => ResolveImportStatus(job),
            _ => StageProgressStatus.Pending
        };
    }

    private static string ResolvePreProcessingStatus(FileJob job, int errorCount)
    {
        if (job.Status == FileJobStatus.PreProcessing)
        {
            return StageProgressStatus.Running;
        }

        if (job.Status == FileJobStatus.ValidationFailed && errorCount > 0)
        {
            return StageProgressStatus.Failed;
        }

        return job.Status is FileJobStatus.Validating
            or FileJobStatus.ValidationFailed
            or FileJobStatus.ReadyToImport
            or FileJobStatus.Importing
            or FileJobStatus.Completed
            ? StageProgressStatus.Completed
            : StageProgressStatus.Pending;
    }

    private static string ResolveValidationStatus(FileJob job, int errorCount)
    {
        if (job.Status == FileJobStatus.Validating)
        {
            return StageProgressStatus.Running;
        }

        if (job.Status == FileJobStatus.ValidationFailed && errorCount > 0)
        {
            return StageProgressStatus.Failed;
        }

        return job.Status is FileJobStatus.ReadyToImport
            or FileJobStatus.Importing
            or FileJobStatus.Completed
            ? StageProgressStatus.Completed
            : StageProgressStatus.Pending;
    }

    private static string ResolveImportStatus(FileJob job)
    {
        return job.Status switch
        {
            FileJobStatus.Importing => StageProgressStatus.Running,
            FileJobStatus.Completed => StageProgressStatus.Completed,
            _ => StageProgressStatus.Pending
        };
    }

    private static int ResolveStageProgress(FileJob job, string stage)
    {
        return stage switch
        {
            ImportProcessingStages.PreProcessing when job.Status == FileJobStatus.PreProcessing => ClampProgress(job.ProgressPercent),
            ImportProcessingStages.Validation when job.Status == FileJobStatus.Validating => ClampProgress(job.ProgressPercent),
            ImportProcessingStages.Import when job.Status == FileJobStatus.Importing => ClampProgress(job.ProgressPercent),
            ImportProcessingStages.PreProcessing when IsPreProcessingComplete(job.Status) => StageProgressPercent.Complete,
            ImportProcessingStages.Validation when IsValidationComplete(job.Status) => StageProgressPercent.Complete,
            ImportProcessingStages.Import when job.Status == FileJobStatus.Completed => StageProgressPercent.Complete,
            _ => StageProgressPercent.Pending
        };
    }

    private static bool IsPreProcessingComplete(FileJobStatus status)
    {
        return status is FileJobStatus.Validating
            or FileJobStatus.ValidationFailed
            or FileJobStatus.ReadyToImport
            or FileJobStatus.Importing
            or FileJobStatus.Completed;
    }

    private static bool IsValidationComplete(FileJobStatus status)
    {
        return status is FileJobStatus.ReadyToImport
            or FileJobStatus.Importing
            or FileJobStatus.Completed;
    }

    private static bool IsCurrentFailureStage(FileJob job, string stage)
    {
        var step = job.CurrentStep;

        return stage switch
        {
            ImportProcessingStages.Import => step.Contains("import", StringComparison.OrdinalIgnoreCase),
            ImportProcessingStages.Validation => step.Contains("valid", StringComparison.OrdinalIgnoreCase),
            ImportProcessingStages.PreProcessing => step.Contains("pre", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static int ClampProgress(int value)
    {
        return Math.Clamp(value, StageProgressPercent.Pending, StageProgressPercent.Complete);
    }

    private static string NormalizeStageCode(string? stage)
    {
        return string.IsNullOrWhiteSpace(stage) ? ImportProcessingStages.Validation : stage.Trim().ToUpperInvariant();
    }

    private static class StageProgressStatus
    {
        public const string Pending = "pending";
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }

    private static class StageProgressPercent
    {
        public const int Pending = 0;
        public const int Complete = 100;
    }
}
