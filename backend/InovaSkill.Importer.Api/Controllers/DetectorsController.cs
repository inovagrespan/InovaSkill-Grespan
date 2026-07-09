using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/detectors")]
public sealed class DetectorsController(
    ImportDbContext dbContext,
    IDetectionJobDispatcher detectionJobDispatcher) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var detectors = await dbContext.DetectorDefinitions.AsNoTracking()
            .Select(d => new
            {
                d.Id,
                d.Code,
                d.Name,
                d.Description,
                status = d.Status.ToString(),
                d.CreatedAt,
                d.UpdatedAt,
                lastRun = dbContext.DetectionRuns.AsNoTracking()
                    .Where(r => r.DetectorDefinitionId == d.Id)
                    .OrderByDescending(r => r.RequestedAt)
                    .Select(r => new
                    {
                        r.Id,
                        status = r.Status.ToString(),
                        r.RequestedAt,
                        r.FindingsCount,
                        r.AnalyzedItems
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(detectors);
    }

    [HttpPost("{detectorId:guid}/runs")]
    public async Task<ActionResult> CreateRun(
        Guid detectorId,
        CancellationToken cancellationToken)
    {
        var detector = await dbContext.DetectorDefinitions
            .SingleOrDefaultAsync(x => x.Id == detectorId, cancellationToken);

        if (detector is null)
            return NotFound(new { message = "Detector não encontrado." });

        if (detector.Status == DetectorStatus.Disabled)
            return Conflict(new { message = "Detector desativado." });

        var hasActiveRun = await dbContext.DetectionRuns.AnyAsync(
            x => x.DetectorDefinitionId == detectorId &&
                 (x.Status == DetectionRunStatus.Queued ||
                  x.Status == DetectionRunStatus.Running),
            cancellationToken);

        if (hasActiveRun)
            return Conflict(new { message = "Este detector já possui uma execução em andamento." });

        var now = DateTime.UtcNow;
        var run = new DetectionRun
        {
            Id = Guid.NewGuid(),
            DetectorDefinitionId = detectorId,
            Status = DetectionRunStatus.Queued,
            Trigger = DetectionTrigger.Manual,
            RequestedAt = now,
            AttemptCount = 0
        };
        dbContext.DetectionRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            detectionJobDispatcher.Enqueue(run.Id);
        }
        catch (Exception exception)
        {
            run.Status = DetectionRunStatus.Failed;
            run.StatusReason = $"Falha ao enfileirar no Hangfire: {exception.Message}";
            run.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return Accepted(new { runId = run.Id, status = run.Status.ToString() });
    }

    [HttpGet("{detectorId:guid}/runs")]
    public async Task<ActionResult> ListRuns(
        Guid detectorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = dbContext.DetectionRuns.AsNoTracking()
            .Where(x => x.DetectorDefinitionId == detectorId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                status = x.Status.ToString(),
                trigger = x.Trigger.ToString(),
                x.RequestedAt,
                x.StartedAt,
                x.FinishedAt,
                x.AttemptCount,
                x.AnalyzedItems,
                x.FindingsCount,
                x.StatusReason
            })
            .ToListAsync(cancellationToken);

        return Ok(new { page, pageSize, total, items });
    }
}
