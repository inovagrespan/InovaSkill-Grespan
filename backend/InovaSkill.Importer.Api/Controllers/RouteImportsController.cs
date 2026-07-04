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
    IMessageBus messageBus) : ControllerBase
{
    private const long MaximumFileSizeBytes = 50 * 1024 * 1024;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    [HttpPost]
    [RequestSizeLimit(MaximumFileSizeBytes)]
    public async Task<ActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Selecione um arquivo XLSX válido." });
        }

        var dataSource = await EnsureDataSourceAsync(cancellationToken);
        await using var content = file.OpenReadStream();
        var storageKey = await fileStorage.SaveAsync(content, file.FileName, cancellationToken);
        var now = DateTime.UtcNow;
        var import = new RouteImport
        {
            Id = Guid.NewGuid(),
            DataSourceId = dataSource.Id,
            FileName = Path.GetFileName(file.FileName),
            FilePath = storageKey,
            Status = RouteImportStatus.Queued,
            CreatedAt = now
        };
        var job = CreateJob(import.Id, now);
        dbContext.RouteImports.Add(import);
        dbContext.JobExecutions.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        await messageBus.PublishAsync(new ProcessImport(import.Id, job.Id));

        return Accepted(new { importId = import.Id, status = "QUEUED" });
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
            .Select(x => ToImportDto(x))
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
        if (!IsValidCorrection(error.Field, request.CorrectedValue))
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

    private async Task<DataSource> EnsureDataSourceAsync(CancellationToken cancellationToken)
    {
        var existing = await dbContext.DataSources
            .SingleOrDefaultAsync(x => x.Code == RouteImportCodes.DataSource, cancellationToken);
        if (existing is not null) return existing;
        var now = DateTime.UtcNow;
        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            Code = RouteImportCodes.DataSource,
            Name = RouteImportCodes.DataSourceName,
            Type = RouteImportCodes.DataSourceType,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.DataSources.Add(dataSource);
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

    private static object ToImportDto(RouteImport item) => new
    {
        item.Id,
        item.FileName,
        status = item.Status.ToString(),
        item.CreatedAt,
        item.TotalRows,
        item.ImportedRows,
        item.ErrorCount,
        durationSeconds = item.StartedAt.HasValue && item.FinishedAt.HasValue
            ? (double?)(item.FinishedAt.Value - item.StartedAt.Value).TotalSeconds : null
    };

    private static bool IsValidCorrection(string field, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return field switch
        {
            "deliveries" => int.TryParse(value, NumberStyles.Integer, CultureInfo.GetCultureInfo("pt-BR"), out var deliveries)
                && deliveries >= 0,
            "average_per_day" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var average)
                && average >= 0,
            "vehicle_type" => value is "Truck" or "Toco" or "Acelo",
            _ => false
        };
    }
}

public sealed record ResolveImportErrorRequest(string CorrectedValue);
