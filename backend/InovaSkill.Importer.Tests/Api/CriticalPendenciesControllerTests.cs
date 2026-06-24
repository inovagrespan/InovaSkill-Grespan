using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InovaSkill.Importer.Tests.Api;

public sealed class CriticalPendenciesControllerTests
{
    [Fact]
    public async Task GetPendencies_ReturnsEmpty_WhenNoneExist()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.GetPendencies(null, null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsType<List<CriticalPendingSummaryDto>>(ok.Value);
        Assert.Empty(items);
    }

    [Fact]
    public async Task CreatePending_PersistsPendingAndNotifiesResponsible()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);
        var request = new CreateCriticalPendingRequestDto(
            "Pendência crítica",
            "Descrição detalhada",
            PendingOrigin.DirectorManual,
            "Produção",
            2,
            PendingPriority.Critical,
            5,
            null, null, null);

        var result = await controller.CreatePending(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var pending = Assert.IsType<CriticalPendingDetailDto>(ok.Value);
        Assert.Equal("Pendência crítica", pending.Title);
        Assert.Equal(PendingStatus.New, pending.Status);

        var saved = await db.CriticalPendencies.FirstAsync(p => p.Id == pending.Id);
        Assert.Equal("Pendência crítica", saved.Title);

        var notification = await db.Notifications.FirstAsync();
        Assert.Equal(2, notification.UserId);
    }

    [Fact]
    public async Task UpdateStatus_ResolvesPending()
    {
        await using var db = CreateDb();
        var pending = new CriticalPending
        {
            Title = "Test",
            Description = "Desc",
            Origin = PendingOrigin.DirectorManual,
            Priority = PendingPriority.Medium,
            Status = PendingStatus.New,
            DeadlineDays = 5,
            CreatedAt = DateTime.UtcNow
        };
        db.CriticalPendencies.Add(pending);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = new UpdatePendingStatusRequestDto(PendingStatus.Resolved, "");

        await controller.UpdateStatus(pending.Id, request);

        var saved = await db.CriticalPendencies.FirstAsync(p => p.Id == pending.Id);
        Assert.Equal(PendingStatus.Resolved, saved.Status);
        Assert.NotNull(saved.ResolvedAt);
    }

    [Fact]
    public async Task GetUnresolvedPendencies_ReturnsOnlyActiveOnes()
    {
        await using var db = CreateDb();
        db.CriticalPendencies.AddRange(
            new CriticalPending { Title = "A", Description = "D", Origin = PendingOrigin.DirectorManual, Priority = PendingPriority.Medium, Status = PendingStatus.New, DeadlineDays = 5, CreatedAt = DateTime.UtcNow },
            new CriticalPending { Title = "B", Description = "D", Origin = PendingOrigin.DirectorManual, Priority = PendingPriority.Medium, Status = PendingStatus.Resolved, DeadlineDays = 5, CreatedAt = DateTime.UtcNow, ResolvedAt = DateTime.UtcNow },
            new CriticalPending { Title = "C", Description = "D", Origin = PendingOrigin.DirectorManual, Priority = PendingPriority.Medium, Status = PendingStatus.Overdue, DeadlineDays = 5, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetUnresolvedPendencies();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsType<List<CriticalPendingSummaryDto>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Title == "A");
        Assert.Contains(items, i => i.Title == "C");
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"critical-pendencies-test-{Guid.NewGuid():N}")
            .Options;
        return new ImportDbContext(options);
    }

    private static CriticalPendenciesController CreateController(ImportDbContext db, long userId = 1)
    {
        var controller = new CriticalPendenciesController(db);
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
