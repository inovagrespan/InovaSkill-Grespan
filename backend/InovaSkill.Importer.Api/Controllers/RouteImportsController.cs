using System.Globalization;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/route-imports")]
public sealed class RouteImportsController(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    ISpreadsheetDataSourceDetector dataSourceDetector,
    IImportLifecycleService importLifecycle,
    IBackgroundJobDispatcher backgroundJobDispatcher) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    [HttpPost]
    [RequestSizeLimit(RouteImportCodes.MaximumUploadSizeBytes)]
    public async Task<ActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName);
        if (file.Length == 0 ||
            (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "Selecione um arquivo XLSX ou CSV válido." });
        }
        string sourceCode;
        try
        {
            if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            {
                sourceCode = HereCustomerCoordinateImportCodes.DataSource;
            }
            else
            {
                await using var detectionContent = file.OpenReadStream();
                sourceCode = dataSourceDetector.Detect(detectionContent);
            }
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
        try
        {
            backgroundJobDispatcher.EnqueueImport(import.Id, job.Id);
        }
        catch (Exception exception)
        {
            job.Status = JobExecutionStatus.Failed;
            job.ErrorMessage = $"Falha ao enfileirar a importação no Hangfire: {exception.Message}";
            job.FinishedAt = DateTime.UtcNow;
            import.Status = RouteImportStatus.Failed;
            import.FailureMessage = "Não foi possível enfileirar o processamento.";
            import.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

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
                item.StartedAt,
                item.TotalRows,
                item.ImportedRows,
                item.ErrorCount,
                item.FailureMessage,
                technicalFailureMessage = item.JobExecutions.OrderByDescending(job => job.CreatedAt)
                    .Select(job => job.ErrorMessage).FirstOrDefault(),
                durationSeconds = item.StartedAt.HasValue
                    ? (double?)((item.FinishedAt ?? DateTime.UtcNow) - item.StartedAt.Value).TotalSeconds
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
                durationSeconds = x.StartedAt.HasValue
                    ? (double?)((x.FinishedAt ?? DateTime.UtcNow) - x.StartedAt.Value).TotalSeconds : null,
                x.TotalRows,
                x.ImportedRows,
                x.ErrorCount,
                x.FailureMessage,
                technicalFailureMessage = x.JobExecutions.OrderByDescending(job => job.CreatedAt)
                    .Select(job => job.ErrorMessage).FirstOrDefault()
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

    [HttpGet("/api/import-errors/{errorId:guid}/candidates")]
    public async Task<ActionResult> ErrorCandidates(
        Guid errorId, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var error = await dbContext.RouteImportErrors.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == errorId, cancellationToken);
        if (error is null) return NotFound();
        var term = search?.Trim().ToUpperInvariant();
        if (error.Field == CustomerRouteAssignmentImportCodes.CustomerCorrectionField)
        {
            var query = dbContext.CustomerSnapshots.AsNoTracking()
                .Where(x => x.Import!.DataSource!.CurrentImportId == x.ImportId);
            if (!string.IsNullOrWhiteSpace(term))
                query = query.Where(x => x.Customer!.ExternalCode.ToUpper().Contains(term) ||
                    x.TradeName.ToUpper().Contains(term) || x.LegalName.ToUpper().Contains(term));
            var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(x => x.TradeName).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new { id = x.CustomerId, label = x.TradeName + " · " + x.Municipality!.Name,
                    detail = "Código " + x.Customer!.ExternalCode + "/" + x.Customer.BranchCode }).ToListAsync(cancellationToken);
            return Ok(new { page, pageSize, total, items });
        }
        if (error.Field == CustomerRouteAssignmentImportCodes.RouteCorrectionField)
        {
            var query = dbContext.Routes.AsNoTracking()
                .Where(x => x.Import!.DataSource!.CurrentImportId == x.ImportId);
            if (!string.IsNullOrWhiteSpace(term)) query = query.Where(x => x.Name.ToUpper().Contains(term));
            var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(x => x.Weekday).ThenBy(x => x.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new { id = x.Id, label = x.Name, detail = x.Weekday }).ToListAsync(cancellationToken);
            return Ok(new { page, pageSize, total, items });
        }
        return BadRequest(new { message = "Esta pendência não possui candidatos cadastrados." });
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
        try
        {
            backgroundJobDispatcher.EnqueueImport(import.Id, job.Id);
        }
        catch (Exception exception)
        {
            job.Status = JobExecutionStatus.Failed;
            job.ErrorMessage = $"Falha ao enfileirar a importação no Hangfire: {exception.Message}";
            job.FinishedAt = DateTime.UtcNow;
            import.Status = RouteImportStatus.Failed;
            import.FailureMessage = "Não foi possível enfileirar o processamento.";
            import.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return Accepted(new { importId = import.Id, jobExecutionId = job.Id, status = "QUEUED" });
    }

    private async Task<DataSource> EnsureDataSourceAsync(string sourceCode, CancellationToken cancellationToken)
    {
        var definition = ResolveDataSourceDefinition(sourceCode);
        var existing = await dbContext.DataSources
            .SingleOrDefaultAsync(x => x.Code == definition.Code, cancellationToken);
        if (existing is not null) return existing;
        var now = DateTime.UtcNow;
        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            Code = definition.Code,
            ProcessorKey = definition.ProcessorKey,
            Name = definition.Name,
            Type = definition.Type,
            ImportMode = definition.ImportMode,
            NextImportVersion = RouteImportCodes.InitialVersion,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.DataSources.Add(dataSource);
        await dbContext.SaveChangesAsync(cancellationToken);
        return dataSource;
    }

    private static DataSourceDefinition ResolveDataSourceDefinition(string sourceCode) =>
        sourceCode.ToUpperInvariant() switch
        {
            CustomerImportCodes.DataSource => new(CustomerImportCodes.DataSource, CustomerImportCodes.ProcessorKey,
                CustomerImportCodes.DataSourceName, CustomerImportCodes.DataSourceType, DataSourceImportMode.Snapshot),
            HereCustomerCoordinateImportCodes.DataSource => new(HereCustomerCoordinateImportCodes.DataSource,
                HereCustomerCoordinateImportCodes.ProcessorKey, HereCustomerCoordinateImportCodes.DataSourceName,
                HereCustomerCoordinateImportCodes.DataSourceType, DataSourceImportMode.Upsert),
            CustomerRouteAssignmentImportCodes.DataSource => new(CustomerRouteAssignmentImportCodes.DataSource,
                CustomerRouteAssignmentImportCodes.ProcessorKey, CustomerRouteAssignmentImportCodes.DataSourceName,
                CustomerRouteAssignmentImportCodes.DataSourceType, DataSourceImportMode.Snapshot),
            FiscalImportCodes.DataSource => new(FiscalImportCodes.DataSource, FiscalImportCodes.ProcessorKey,
                FiscalImportCodes.DataSourceName, FiscalImportCodes.DataSourceType, DataSourceImportMode.Upsert),
            ProductImportCodes.DataSource => new(ProductImportCodes.DataSource, ProductImportCodes.ProcessorKey,
                ProductImportCodes.DataSourceName, ProductImportCodes.DataSourceType, DataSourceImportMode.Upsert),
            InventoryCurrentImportCodes.DataSource => new(InventoryCurrentImportCodes.DataSource,
                InventoryCurrentImportCodes.ProcessorKey, InventoryCurrentImportCodes.DataSourceName,
                InventoryCurrentImportCodes.DataSourceType, DataSourceImportMode.Snapshot),
            DailyInventoryImportCodes.DataSource => new(DailyInventoryImportCodes.DataSource,
                DailyInventoryImportCodes.ProcessorKey, DailyInventoryImportCodes.DataSourceName,
                DailyInventoryImportCodes.DataSourceType, DataSourceImportMode.Snapshot),
            _ => new(RouteImportCodes.DataSource, RouteImportCodes.ProcessorKey, RouteImportCodes.DataSourceName,
                RouteImportCodes.DataSourceType, DataSourceImportMode.Snapshot)
        };

    private static JobExecution CreateJob(Guid importId, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        JobType = RouteImportCodes.JobType,
        ContractVersion = 1,
        Queue = BackgroundJobQueues.Imports,
        Trigger = JobExecutionTrigger.Import,
        ParametersJson = JsonSerializer.Serialize(new { importId }),
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
            CustomerRouteAssignmentImportCodes.CustomerCorrectionField => Guid.TryParse(value, out var customerId) &&
                await dbContext.CustomerSnapshots.AnyAsync(x => x.CustomerId == customerId &&
                    x.Import!.DataSource!.CurrentImportId == x.ImportId, cancellationToken),
            CustomerRouteAssignmentImportCodes.RouteCorrectionField => Guid.TryParse(value, out var routeId) &&
                await dbContext.Routes.AnyAsync(x => x.Id == routeId &&
                    x.Import!.DataSource!.CurrentImportId == x.ImportId, cancellationToken),
            "weekday" => CustomerRouteAssignmentsSpreadsheetParser.IsSupportedWeekday(value.Trim().ToUpperInvariant()),
            "market_name" or "route_name" or "municipality_name" => !string.IsNullOrWhiteSpace(value),
            _ => false
        };
    }
}

public sealed record ResolveImportErrorRequest(string CorrectedValue);

sealed record DataSourceDefinition(
    string Code,
    string ProcessorKey,
    string Name,
    string Type,
    DataSourceImportMode ImportMode);
