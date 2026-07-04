using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/admin/jobs")]
public sealed class AdminJobsController(ImportDbContext dbContext, IMessageBus messageBus) : ControllerBase
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
                status = x.Status.ToString(),
                importId = x.RelatedEntityId,
                importFileName = x.Import!.FileName,
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
                status = x.Status.ToString(),
                importId = x.RelatedEntityId,
                x.Import!.FileName,
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
        if (failedJob.Status != JobExecutionStatus.Failed || failedJob.Import!.Status == RouteImportStatus.NeedsReview)
        {
            return Conflict(new { message = "Apenas jobs técnicos com falha podem ser reenviados." });
        }

        var newJob = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobType = failedJob.JobType,
            Status = JobExecutionStatus.Queued,
            RelatedEntityId = failedJob.RelatedEntityId,
            CreatedAt = DateTime.UtcNow
        };
        failedJob.Import.Status = RouteImportStatus.Queued;
        failedJob.Import.StartedAt = null;
        failedJob.Import.FinishedAt = null;
        failedJob.Import.FailureMessage = null;
        dbContext.JobExecutions.Add(newJob);
        await dbContext.SaveChangesAsync(cancellationToken);
        await messageBus.PublishAsync(new ProcessImport(newJob.RelatedEntityId, newJob.Id));
        return Accepted(new { jobExecutionId = newJob.Id, status = "QUEUED" });
    }
}
