using System.Security.Claims;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(ImportDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationListDto>> GetNotifications(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var current = CurrentUser();
        var query = dbContext.Notifications.AsNoTracking().Where(n => n.UserId == current.Id);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(n => n.Status == status);

        var total = await query.CountAsync(ct);
        var unreadCount = await dbContext.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == current.Id && n.Status == NotificationStatus.Unread, ct);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id, n.UserId, n.Title, n.Message, n.Type, n.Priority, n.Status,
                n.RelatedLink, n.RelatedEntity, n.RelatedEntityId, n.CreatedAt, n.ReadAt))
            .ToListAsync(ct);

        return Ok(new NotificationListDto(total, unreadCount, notifications));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(CancellationToken ct = default)
    {
        var current = CurrentUser();
        var count = await dbContext.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == current.Id && n.Status == NotificationStatus.Unread, ct);
        return Ok(new UnreadCountDto(count));
    }

    [HttpPut("{id:long}/read")]
    public async Task<ActionResult> MarkAsRead(long id, CancellationToken ct = default)
    {
        var notification = await dbContext.Notifications.FindAsync([id], ct);
        if (notification is null) return NotFound();

        notification.Status = NotificationStatus.Read;
        notification.ReadAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPut("read-all")]
    public async Task<ActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var current = CurrentUser();
        await dbContext.Notifications
            .Where(n => n.UserId == current.Id && n.Status == NotificationStatus.Unread)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.Status, NotificationStatus.Read)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
        return Ok();
    }

    [HttpPut("{id:long}/archive")]
    public async Task<ActionResult> Archive(long id, CancellationToken ct = default)
    {
        var notification = await dbContext.Notifications.FindAsync([id], ct);
        if (notification is null) return NotFound();

        notification.Status = NotificationStatus.Archived;
        await dbContext.SaveChangesAsync(ct);
        return Ok();
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
