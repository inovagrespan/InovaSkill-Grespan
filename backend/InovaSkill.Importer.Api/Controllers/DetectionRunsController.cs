using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/detection-runs")]
public sealed class DetectionRunsController(
    ImportDbContext dbContext) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    [HttpGet("{runId:guid}")]
    public async Task<ActionResult> Get(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.DetectionRuns.AsNoTracking()
            .Where(x => x.Id == runId)
            .Select(x => new
            {
                x.Id,
                detector = new
                {
                    x.DetectorDefinition!.Id,
                    x.DetectorDefinition.Code,
                    x.DetectorDefinition.Name
                },
                status = x.Status.ToString(),
                trigger = x.Trigger.ToString(),
                x.RequestedAt,
                x.StartedAt,
                x.FinishedAt,
                x.AttemptCount,
                x.AnalyzedItems,
                x.FindingsCount,
                x.StatusReason,
                durationSeconds = x.StartedAt.HasValue && x.FinishedAt.HasValue
                    ? (double?)(x.FinishedAt.Value - x.StartedAt.Value).TotalSeconds
                    : null
            })
            .SingleOrDefaultAsync(cancellationToken);

        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("{runId:guid}/findings")]
    public async Task<ActionResult> ListFindings(
        Guid runId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = dbContext.Findings.AsNoTracking()
            .Where(x => x.DetectionRunId == runId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.SubjectLabel)
            .ThenBy(x => x.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Fingerprint,
                x.Title,
                x.Description,
                x.SubjectType,
                x.SubjectId,
                x.SubjectLabel,
                x.DetectedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { page, pageSize, total, items });
    }
}
