using System.Security.Claims;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/critical-pendencies")]
public sealed class CriticalPendenciesController(ImportDbContext dbContext) : ControllerBase
{
    private const string DirectorRole = "diretor";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CriticalPendingSummaryDto>>> GetPendencies(
        [FromQuery] string? status,
        [FromQuery] string? sector,
        CancellationToken ct = default)
    {
        var query = dbContext.CriticalPendencies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(sector))
            query = query.Where(p => p.Sector == sector);

        var result = await query
            .OrderByDescending(p => p.Priority).ThenBy(p => p.DeadlineAt).ThenByDescending(p => p.CreatedAt)
            .Select(p => new CriticalPendingSummaryDto(
                p.Id, p.Title, p.Description, p.Origin, p.Sector, p.ResponsibleName,
                p.Priority, p.Status, p.DeadlineDays, p.DeadlineAt, p.CreatedAt))
            .ToListAsync(ct);

        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CriticalPendingDetailDto>> GetPending(long id, CancellationToken ct = default)
    {
        var pending = await dbContext.CriticalPendencies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pending is null) return NotFound();

        return Ok(new CriticalPendingDetailDto(
            pending.Id, pending.Title, pending.Description, pending.Origin, pending.Sector,
            pending.ResponsibleUserId, pending.ResponsibleName, pending.Priority, pending.Status,
            pending.DeadlineDays, pending.SourceMeetingId, pending.RelatedActionId, pending.RelatedDecisionId,
            pending.NotificationHistoryJson, pending.EscalationHistoryJson,
            pending.CreatedAt, pending.ResolvedAt, pending.DeadlineAt, pending.AiSuggestion));
    }

    [HttpPost]
    public async Task<ActionResult<CriticalPendingDetailDto>> CreatePending([FromBody] CreateCriticalPendingRequestDto request, CancellationToken ct = default)
    {
        var current = CurrentUser();
        var now = DateTime.UtcNow;

        var pending = new CriticalPending
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Origin = string.IsNullOrWhiteSpace(request.Origin) ? PendingOrigin.DirectorManual : request.Origin,
            Sector = request.Sector ?? "",
            ResponsibleUserId = request.ResponsibleUserId,
            Priority = request.Priority,
            Status = PendingStatus.New,
            DeadlineDays = request.DeadlineDays,
            SourceMeetingId = request.SourceMeetingId,
            RelatedActionId = request.RelatedActionId,
            RelatedDecisionId = request.RelatedDecisionId,
            CreatedAt = now,
            DeadlineAt = request.DeadlineDays > 0 ? now.AddDays(request.DeadlineDays) : null
        };

        dbContext.CriticalPendencies.Add(pending);
        await dbContext.SaveChangesAsync(ct);

        if (request.ResponsibleUserId.HasValue)
        {
            dbContext.Notifications.Add(new Notification
            {
                UserId = request.ResponsibleUserId.Value,
                Title = "Pendência crítica",
                Message = $"Uma pendência crítica foi registrada: {pending.Title}.",
                Type = NotificationType.CriticalPendingAlert,
                Priority = NotificationPriority.Critical,
                RelatedLink = $"/pendencias/{pending.Id}",
                RelatedEntity = "CriticalPending",
                RelatedEntityId = pending.Id,
                CreatedAt = now
            });
            await dbContext.SaveChangesAsync(ct);
        }

        return Ok(new CriticalPendingDetailDto(
            pending.Id, pending.Title, pending.Description, pending.Origin, pending.Sector,
            pending.ResponsibleUserId, pending.ResponsibleName, pending.Priority, pending.Status,
            pending.DeadlineDays, pending.SourceMeetingId, pending.RelatedActionId, pending.RelatedDecisionId,
            pending.NotificationHistoryJson, pending.EscalationHistoryJson,
            pending.CreatedAt, pending.ResolvedAt, pending.DeadlineAt, pending.AiSuggestion));
    }

    [HttpPut("{id:long}/status")]
    public async Task<ActionResult> UpdateStatus(long id, [FromBody] UpdatePendingStatusRequestDto request, CancellationToken ct = default)
    {
        var pending = await dbContext.CriticalPendencies.FindAsync([id], ct);
        if (pending is null) return NotFound();

        pending.Status = request.Status;
        if (request.Status == PendingStatus.Resolved)
            pending.ResolvedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpGet("unresolved")]
    public async Task<ActionResult<IReadOnlyList<CriticalPendingSummaryDto>>> GetUnresolvedPendencies(CancellationToken ct = default)
    {
        var current = CurrentUser();
        var now = DateTime.UtcNow;

        var result = await dbContext.CriticalPendencies
            .AsNoTracking()
            .Where(p => p.Status != PendingStatus.Resolved && p.Status != PendingStatus.CancelledWithJustification)
            .OrderByDescending(p => p.Priority).ThenBy(p => p.DeadlineAt)
            .Select(p => new CriticalPendingSummaryDto(
                p.Id, p.Title, p.Description, p.Origin, p.Sector, p.ResponsibleName,
                p.Priority, p.Status, p.DeadlineDays, p.DeadlineAt, p.CreatedAt))
            .ToListAsync(ct);

        return Ok(result);
    }

    private (long Id, string? Name, string? Email, string Role) CurrentUser()
    {
        var claims = User;
        var idClaim = claims.FindFirstValue("sub") ?? claims.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(idClaim, out var userId);
        return (userId,
            claims.FindFirstValue("name") ?? claims.FindFirstValue(ClaimTypes.Name),
            claims.FindFirstValue("email") ?? claims.FindFirstValue(ClaimTypes.Email),
            claims.FindFirstValue("role") ?? claims.FindFirstValue(ClaimTypes.Role) ?? "");
    }
}
