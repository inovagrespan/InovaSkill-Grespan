using System.Globalization;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/route-imports")]
public sealed class RouteImportsController(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    ISpreadsheetDataSourceDetector dataSourceDetector,
    IImportLifecycleService importLifecycle,
    IMessageBus messageBus) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    [HttpPost]
    [RequestSizeLimit(RouteImportCodes.MaximumUploadSizeBytes)]
    public async Task<ActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0 || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Selecione um arquivo XLSX válido." });
        }
        string sourceCode;
        try
        {
            await using var detectionContent = file.OpenReadStream();
            sourceCode = dataSourceDetector.Detect(detectionContent);
        }
        catch (StructuralImportException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        var dataSource = await EnsureDataSourceAsync(sourceCode, cancellationToken);
        await using var content = file.OpenReadStream();
        var storageKey = await fileStorage.SaveAsync(content, file.FileName, cancellationToken);
        var import = await importLifecycle.CreateAsync(
            dataSource.Id,
            Path.GetFileName(file.FileName),
            storageKey,
            cancellationToken);
        var job = CreateJob(import.Id, import.CreatedAt);
        dbContext.JobExecutions.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        await messageBus.PublishAsync(new ProcessImport(import.Id, job.Id));

        return Accepted(new { importId = import.Id, sourceCode, status = "QUEUED" });
    }

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var query = dbContext.RouteImports.AsNoTracking().OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.FileName,
                sourceCode = item.DataSource!.Code,
                sourceName = item.DataSource.Name,
                item.Version,
                isCurrent = item.DataSource!.CurrentImportId == item.Id,
                status = item.Status.ToString(),
                item.CreatedAt,
                item.TotalRows,
                item.ImportedRows,
                item.ErrorCount,
                durationSeconds = item.StartedAt.HasValue && item.FinishedAt.HasValue
                    ? (double?)(item.FinishedAt.Value - item.StartedAt.Value).TotalSeconds
                    : null
            })
            .ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.RouteImports.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.FileName,
                x.Version,
                source = x.DataSource!.Name,
                status = x.Status.ToString(),
                x.CreatedAt,
                x.StartedAt,
                x.FinishedAt,
                durationSeconds = x.StartedAt.HasValue && x.FinishedAt.HasValue
                    ? (double?)(x.FinishedAt.Value - x.StartedAt.Value).TotalSeconds : null,
                x.TotalRows,
                x.ImportedRows,
                x.ErrorCount,
                x.FailureMessage
            })
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/errors")]
    public async Task<ActionResult> Errors(Guid id, CancellationToken cancellationToken)
    {
        var errors = await dbContext.RouteImportErrors.AsNoTracking()
            .Where(x => x.ImportId == id)
            .OrderBy(x => x.SheetName).ThenBy(x => x.RowNumber).ThenBy(x => x.Field)
            .Select(x => new
            {
                x.Id,
                x.SheetName,
                x.RowNumber,
                x.Field,
                x.RawValue,
                x.Message,
                status = x.Status.ToString(),
                x.CorrectedValue,
                x.ResolvedAt
            })
            .ToListAsync(cancellationToken);
        return Ok(errors);
    }

    [HttpPost("/api/import-errors/{errorId:guid}/resolve")]
    public async Task<ActionResult> ResolveError(
        Guid errorId,
        [FromBody] ResolveImportErrorRequest request,
        CancellationToken cancellationToken)
    {
        var error = await dbContext.RouteImportErrors.SingleOrDefaultAsync(x => x.Id == errorId, cancellationToken);
        if (error is null) return NotFound();
        if (!await IsValidCorrectionAsync(error.Field, request.CorrectedValue, cancellationToken))
        {
            return BadRequest(new { message = "O valor corrigido não é válido para o campo." });
        }

        error.CorrectedValue = request.CorrectedValue.Trim();
        error.Status = ImportErrorStatus.Resolved;
        error.ResolvedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPost("{id:guid}/reprocess")]
    public async Task<ActionResult> Reprocess(Guid id, CancellationToken cancellationToken)
    {
        var import = await dbContext.RouteImports.Include(x => x.Errors)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (import is null) return NotFound();
        if (import.Status != RouteImportStatus.NeedsReview || import.Errors.Any(x => x.Status == ImportErrorStatus.Pending))
        {
            return Conflict(new { message = "Resolva todos os erros pendentes antes de reprocessar." });
        }

        return await QueueNewExecution(import, cancellationToken);
    }

    private async Task<ActionResult> QueueNewExecution(RouteImport import, CancellationToken cancellationToken)
    {
        var job = CreateJob(import.Id, DateTime.UtcNow);
        import.Status = RouteImportStatus.Queued;
        import.StartedAt = null;
        import.FinishedAt = null;
        import.FailureMessage = null;
        dbContext.JobExecutions.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        await messageBus.PublishAsync(new ProcessImport(import.Id, job.Id));
        return Accepted(new { importId = import.Id, jobExecutionId = job.Id, status = "QUEUED" });
    }

    private async Task<DataSource> EnsureDataSourceAsync(string sourceCode, CancellationToken cancellationToken)
    {
        var isCustomers = string.Equals(sourceCode, CustomerImportCodes.DataSource, StringComparison.OrdinalIgnoreCase);
        var isFiscal = string.Equals(sourceCode, FiscalImportCodes.DataSource, StringComparison.OrdinalIgnoreCase);
        var code = isCustomers ? CustomerImportCodes.DataSource : isFiscal ? FiscalImportCodes.DataSource : RouteImportCodes.DataSource;
        var existing = await dbContext.DataSources
            .SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (existing is not null) return existing;
        var now = DateTime.UtcNow;
        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            Code = code,
            ProcessorKey = isCustomers ? CustomerImportCodes.ProcessorKey : isFiscal ? FiscalImportCodes.ProcessorKey : RouteImportCodes.ProcessorKey,
            Name = isCustomers ? CustomerImportCodes.DataSourceName : isFiscal ? FiscalImportCodes.DataSourceName : RouteImportCodes.DataSourceName,
            Type = isCustomers ? CustomerImportCodes.DataSourceType : isFiscal ? FiscalImportCodes.DataSourceType : RouteImportCodes.DataSourceType,
            ImportMode = isFiscal ? DataSourceImportMode.Upsert : DataSourceImportMode.Snapshot,
            NextImportVersion = RouteImportCodes.InitialVersion,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.DataSources.Add(dataSource);
        await dbContext.SaveChangesAsync(cancellationToken);
        return dataSource;
    }

    private static JobExecution CreateJob(Guid importId, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        JobType = RouteImportCodes.JobType,
        Status = JobExecutionStatus.Queued,
        RelatedEntityId = importId,
        CreatedAt = now
    };

    private async Task<bool> IsValidCorrectionAsync(string field, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return field switch
        {
            "deliveries" => int.TryParse(value, NumberStyles.Integer, CultureInfo.GetCultureInfo("pt-BR"), out var deliveries)
                && deliveries >= 0,
            "average_per_day" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var average)
                && average >= 0,
            "vehicle_type" => await dbContext.VehicleTypes.AnyAsync(
                x => x.Name == value.Trim(), cancellationToken),
            _ => false
        };
    }
}

public sealed record ResolveImportErrorRequest(string CorrectedValue);
