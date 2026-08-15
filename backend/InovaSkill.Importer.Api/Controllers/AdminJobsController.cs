using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/admin/jobs")]
public sealed class AdminJobsController(
    ImportDbContext dbContext,
    IBackgroundJobDispatcher backgroundJobDispatcher) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    [HttpGet("summary")]
    public async Task<ActionResult> Summary(CancellationToken cancellationToken)
    {
        var jobs = await dbContext.JobExecutions.AsNoTracking().ToListAsync(cancellationToken);
        var summary = JobExecutionSummaryCalculator.Calculate(jobs, DateTime.UtcNow);
        return Ok(new
        {
            queuedNow = summary.QueuedNow,
            processingNow = summary.ProcessingNow,
            completedLast24Hours = summary.CompletedLast24Hours,
            failedLast24Hours = summary.FailedLast24Hours,
            successRatePercent = summary.SuccessRatePercent,
            averageProcessingSeconds = summary.AverageProcessingSeconds
        });
    }

    [HttpGet("definitions")]
    public async Task<ActionResult> Definitions(CancellationToken cancellationToken)
    {
        var runningJobs = await dbContext.JobExecutions.AsNoTracking()
            .Where(job => job.Status == JobExecutionStatus.Queued ||
                job.Status == JobExecutionStatus.Processing ||
                job.Status == JobExecutionStatus.Retrying)
            .GroupBy(job => job.JobType)
            .Select(group => new { JobType = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.JobType, item => item.Count, cancellationToken);

        return Ok(OperationalJobCatalog.All.Select(definition => new
        {
            definition.JobType,
            definition.DisplayName,
            definition.Description,
            definition.ManualRunAllowed,
            definition.ScheduleAllowed,
            definition.AllowConcurrentRuns,
            definition.Queue,
            definition.ContractVersion,
            definition.ExampleParametersJson,
            currentlyRunning = runningJobs.GetValueOrDefault(definition.JobType) > 0
        }));
    }

    [HttpPost("definitions/{jobType}/run")]
    public async Task<ActionResult> RunDefinition(
        string jobType,
        [FromBody] RunJobDefinitionRequest request,
        [FromServices] IJobExecutionLauncher launcher,
        CancellationToken cancellationToken)
    {
        if (!OperationalJobCatalog.TryGet(jobType, out var definition)) return NotFound();
        if (!definition.ManualRunAllowed)
            return Conflict(new { message = "Este job não permite execução manual." });

        try
        {
            var launched = await launcher.LaunchAsync(new JobLaunchRequest(
                definition.JobType,
                request.ContractVersion,
                request.Parameters.GetRawText(),
                JobExecutionTrigger.Manual,
                ReadUserId()), cancellationToken);
            return Accepted(new { jobExecutionId = launched.JobExecutionId, status = launched.Status });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var query = dbContext.JobExecutions.AsNoTracking();
        if (Enum.TryParse<JobExecutionStatus>(status, true, out var parsedStatus)) query = query.Where(x => x.Status == parsedStatus);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.JobType == type);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.CreatedAt <= to.Value);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.JobType,
                x.ContractVersion,
                x.Queue,
                trigger = x.Trigger.ToString(),
                status = x.Status.ToString(),
                importId = x.RelatedEntityId,
                importFileName = x.Import == null ? "Job operacional" : x.Import.FileName,
                x.Attempts,
                x.CreatedAt,
                x.StartedAt,
                x.FinishedAt,
                durationSeconds = x.StartedAt.HasValue && x.FinishedAt.HasValue
                    ? (double?)(x.FinishedAt.Value - x.StartedAt.Value).TotalSeconds : null,
                x.ErrorMessage
            }).ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.JobExecutions.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.JobType,
                x.ContractVersion,
                x.Queue,
                trigger = x.Trigger.ToString(),
                x.ParametersJson,
                x.ResultJson,
                x.ProgressPercent,
                x.ProgressMessage,
                x.CancellationRequestedAt,
                x.RequestedByUserId,
                x.ScheduleId,
                x.RetriedFromJobExecutionId,
                status = x.Status.ToString(),
                importId = x.RelatedEntityId,
                fileName = x.Import == null ? "Job operacional" : x.Import.FileName,
                x.Attempts,
                x.CreatedAt,
                x.StartedAt,
                x.FinishedAt,
                durationSeconds = x.StartedAt.HasValue && x.FinishedAt.HasValue
                    ? (double?)(x.FinishedAt.Value - x.StartedAt.Value).TotalSeconds : null,
                x.ErrorMessage
            }).SingleOrDefaultAsync(cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var failedJob = await dbContext.JobExecutions.Include(x => x.Import)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (failedJob is null) return NotFound();
        if (failedJob.Status != JobExecutionStatus.Failed)
        {
            return Conflict(new { message = "Apenas jobs técnicos com falha podem ser reenviados." });
        }

        var newJob = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobType = failedJob.JobType,
            ContractVersion = failedJob.ContractVersion,
            Queue = failedJob.Queue,
            Trigger = JobExecutionTrigger.Retry,
            ParametersJson = failedJob.ParametersJson,
            RequestedByUserId = ReadUserId(),
            RetriedFromJobExecutionId = failedJob.Id,
            Status = JobExecutionStatus.Queued,
            RelatedEntityId = failedJob.RelatedEntityId,
            CreatedAt = DateTime.UtcNow
        };
        var isOperationalJob = OperationalJobCatalog.All.Any(item => item.JobType == failedJob.JobType);
        if (!isOperationalJob)
        {
            if (failedJob.Import!.Status == RouteImportStatus.NeedsReview)
            {
                return Conflict(new { message = "Apenas jobs técnicos com falha podem ser reenviados." });
            }

            failedJob.Import.Status = RouteImportStatus.Queued;
            failedJob.Import.StartedAt = null;
            failedJob.Import.FinishedAt = null;
            failedJob.Import.FailureMessage = null;
        }
        dbContext.JobExecutions.Add(newJob);
        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            if (isOperationalJob)
                backgroundJobDispatcher.EnqueueOperationalJob(newJob.Id);
            else
                backgroundJobDispatcher.EnqueueImport(newJob.RelatedEntityId, newJob.Id);
        }
        catch (Exception exception)
        {
            newJob.Status = JobExecutionStatus.Failed;
            newJob.ErrorMessage = $"Falha ao reenfileirar o job no Hangfire: {exception.Message}";
            newJob.FinishedAt = DateTime.UtcNow;
            if (!isOperationalJob && failedJob.Import is not null)
            {
                failedJob.Import.Status = RouteImportStatus.Failed;
                failedJob.Import.FailureMessage = "Não foi possível reenfileirar o processamento.";
                failedJob.Import.FinishedAt = DateTime.UtcNow;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return Accepted(new { jobExecutionId = newJob.Id, status = "QUEUED" });
    }

    [HttpPost("{id:guid}/retry-with-parameters")]
    public async Task<ActionResult> RetryWithParameters(
        Guid id,
        [FromBody] RetryJobWithParametersRequest request,
        [FromServices] IJobExecutionLauncher launcher,
        CancellationToken cancellationToken)
    {
        var failedJob = await dbContext.JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (failedJob is null) return NotFound();
        if (failedJob.Status != JobExecutionStatus.Failed)
            return Conflict(new { message = "Apenas jobs com falha podem ser reenviados." });

        try
        {
            var launched = await launcher.LaunchAsync(new JobLaunchRequest(
                failedJob.JobType,
                request.ContractVersion,
                request.Parameters.GetRawText(),
                JobExecutionTrigger.Retry,
                ReadUserId(),
                RetriedFromJobExecutionId: failedJob.Id), cancellationToken);
            return Accepted(new { jobExecutionId = launched.JobExecutionId, status = launched.Status });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private long? ReadUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) ? userId : null;
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.JobExecutions.Include(x => x.Import)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (job is null) return NotFound();
        if (job.Status is not (JobExecutionStatus.Processing or JobExecutionStatus.Retrying or JobExecutionStatus.Queued))
        {
            return Conflict(new { message = "Apenas jobs em execução, na fila ou em retentativa podem ser cancelados." });
        }

        job.CancellationRequestedAt = DateTime.UtcNow;
        job.ProgressMessage = "Cancelamento solicitado";
        if (job.Status == JobExecutionStatus.Queued)
        {
            job.Status = JobExecutionStatus.Cancelled;
            job.FinishedAt = DateTime.UtcNow;
        }

        if (job.Import is not null && job.Import.Status == RouteImportStatus.Processing)
        {
            job.Import.Status = RouteImportStatus.Failed;
            job.Import.FailureMessage = "Cancelado pelo usuário.";
            job.Import.FinishedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { status = job.Status.ToString().ToUpperInvariant() });
    }

    private async Task<Guid?> ResolveOperationalJobRelatedEntityIdAsync(
        string jobType,
        CancellationToken cancellationToken)
    {
        var dataSourceCode = jobType switch
        {
            OperationalJobCodes.MunicipalityCoordinateEnrichment => CustomerImportCodes.DataSource,
            OperationalJobCodes.CustomerRegistrationAddressEnrichment => CustomerImportCodes.DataSource,
            _ => throw new InvalidOperationException($"Job operacional sem resolvedor: {jobType}.")
        };

        var currentImportId = await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == dataSourceCode)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);

        return currentImportId;
    }
}

public sealed record RunJobDefinitionRequest(int ContractVersion, JsonElement Parameters);

public sealed record RetryJobWithParametersRequest(int ContractVersion, JsonElement Parameters);
