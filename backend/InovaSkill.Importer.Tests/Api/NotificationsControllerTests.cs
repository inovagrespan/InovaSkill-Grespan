using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InovaSkill.Importer.Tests.Api;

public sealed class NotificationsControllerTests
{
    [Fact]
    public async Task GetNotifications_ReturnsEmpty_WhenNoNotifications()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.GetNotifications(null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<NotificationListDto>(ok.Value);
        Assert.Equal(0, payload.Total);
        Assert.Empty(payload.Notifications);
    }

    [Fact]
    public async Task CreateNotification_And_GetNotifications_ReturnsIt()
    {
        await using var db = CreateDb();
        db.Notifications.Add(new Notification
        {
            UserId = 1,
            Title = "Test Notification",
            Message = "Test message",
            Type = NotificationType.General,
            Priority = NotificationPriority.Medium,
            Status = NotificationStatus.Unread,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetNotifications(null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<NotificationListDto>(ok.Value);
        Assert.Equal(1, payload.Total);
        Assert.Single(payload.Notifications);
        Assert.Equal("Test Notification", payload.Notifications[0].Title);
    }

    [Fact]
    public async Task MarkAsRead_UpdatesStatus()
    {
        await using var db = CreateDb();
        var notification = new Notification
        {
            UserId = 1, Title = "Test", Message = "Msg",
            Type = NotificationType.General, Priority = NotificationPriority.Medium,
            Status = NotificationStatus.Unread, CreatedAt = DateTime.UtcNow
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        await controller.MarkAsRead(notification.Id);

        var saved = await db.Notifications.FirstAsync(n => n.Id == notification.Id);
        Assert.Equal(NotificationStatus.Read, saved.Status);
        Assert.NotNull(saved.ReadAt);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCorrectCount()
    {
        await using var db = CreateDb();
        db.Notifications.AddRange(
            new Notification { UserId = 1, Title = "A", Message = "M", Type = NotificationType.General, Priority = NotificationPriority.Medium, Status = NotificationStatus.Unread, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = 1, Title = "B", Message = "M", Type = NotificationType.General, Priority = NotificationPriority.Medium, Status = NotificationStatus.Unread, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = 1, Title = "C", Message = "M", Type = NotificationType.General, Priority = NotificationPriority.Medium, Status = NotificationStatus.Read, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetUnreadCount();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var count = Assert.IsType<UnreadCountDto>(ok.Value);
        Assert.Equal(2, count.Count);
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"notifications-test-{Guid.NewGuid():N}")
            .Options;
        return new ImportDbContext(options);
    }

    private static NotificationsController CreateController(ImportDbContext db, long userId = 1)
    {
        var controller = new NotificationsController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("sub", userId.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "TestAuth"))
            }
        };
        return controller;
    }
}
