using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AiConsumptionServiceTests
{
    [Fact]
    public async Task RecordCall_UsesEffectivePriceAndCalculatesPartsAndTotal()
    {
        await using var db = CreateDb();
        db.AppUsers.Add(User(1));
        db.AiModelPrices.AddRange(
            new AiModelPrice { Model = "model-a", InputPricePerMillionUsd = 1m, OutputPricePerMillionUsd = 2m, EffectiveFrom = DateTime.UtcNow.AddDays(-2) },
            new AiModelPrice { Model = "model-a", InputPricePerMillionUsd = 3m, OutputPricePerMillionUsd = 4m, EffectiveFrom = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        Assert.True((await service.BeginAsync(1, AppUserRoles.Vendas, default)).Allowed);
        await service.RecordCallAsync("model-a", ChatModelRequestPurpose.Answer, AiConsumptionStatuses.Completed, "response", 500_000, 250_000, default);

        var call = await db.AiProviderCalls.SingleAsync();
        Assert.Equal(0.5m, call.InputCostUsd);
        Assert.Equal(0.5m, call.OutputCostUsd);
        Assert.Equal(1m, call.InputCostUsd + call.OutputCostUsd);
        Assert.Equal(750_000, call.InputTokens + call.OutputTokens);
    }

    [Theory]
    [InlineData(AppUserRoles.Vendas, false)]
    [InlineData(AppUserRoles.AdminSystem, true)]
    public async Task Begin_RespectsZeroLimitExceptForAdminSystem(string role, bool expected)
    {
        await using var db = CreateDb();
        db.AppUsers.Add(User(1));
        db.AiConsumptionSettings.Add(new AiConsumptionSettings { Id = 1, Model = "model-a", DefaultMonthlyTokenLimit = 0, DefaultAlertPercentage = 80 });
        await db.SaveChangesAsync();

        var admission = await CreateService(db).BeginAsync(1, role, default);

        Assert.Equal(expected, admission.Allowed);
    }

    private static AiConsumptionService CreateService(ImportDbContext db) => new(db, Options.Create(new AssistantOptions { Model = "fallback" }));
    private static ImportDbContext CreateDb() => new(new DbContextOptionsBuilder<ImportDbContext>().UseInMemoryDatabase($"ai-consumption-{Guid.NewGuid()}").Options);
    private static AppUser User(long id) => new() { Id = id, Name = $"User {id}", Email = $"user{id}@test.com", PasswordHash = "hash", Role = AppUserRoles.Vendas };
}
