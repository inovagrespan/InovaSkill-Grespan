using System.Text.Json;
using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AiConsumptionAdminControllerTests
{
    [Fact]
    public async Task Users_ReturnsOnlyRequestedPageWithStableTotal()
    {
        await using var db = CreateDb();
        db.AppUsers.AddRange(Enumerable.Range(1, 25).Select(index => new AppUser
        {
            Id = index,
            Name = $"Usuário {index:D2}",
            Email = $"usuario{index:D2}@test.com",
            PasswordHash = "hash",
            Role = AppUserRoles.Vendas
        }));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Users(null, page: 2, pageSize: 10);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal(25, payload.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(2, payload.RootElement.GetProperty("page").GetInt32());
        var items = payload.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(10, items.Length);
        Assert.Equal("Usuário 11", items[0].GetProperty("Name").GetString());
        Assert.Equal("Usuário 20", items[^1].GetProperty("Name").GetString());
    }

    [Fact]
    public async Task Users_ClampsInvalidPagingInputs()
    {
        await using var db = CreateDb();
        db.AppUsers.Add(new AppUser { Id = 1, Name = "Admin", Email = "admin@test.com", PasswordHash = "hash", Role = AppUserRoles.Admin });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Users(null, page: 0, pageSize: 500);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal(1, payload.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(100, payload.RootElement.GetProperty("pageSize").GetInt32());
    }

    private static AiConsumptionAdminController CreateController(ImportDbContext db) =>
        new(db, new AiConsumptionService(db, Options.Create(new AssistantOptions { Model = "test-model" })));

    private static ImportDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"ai-consumption-admin-{Guid.NewGuid()}")
            .Options);
}
