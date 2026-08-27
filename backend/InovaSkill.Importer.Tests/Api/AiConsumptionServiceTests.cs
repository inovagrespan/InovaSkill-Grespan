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

    [Fact]
    public async Task GetSessionUsage_SumsTokensAndStoredCostsOnlyForOwnedSession()
    {
        await using var db = CreateDb();
        var sessionId = Guid.NewGuid();
        db.AppUsers.AddRange(User(1), User(2));
        db.ChatSessions.Add(new ChatSession { Id = sessionId, UserId = 1, Channel = ChatSessionChannels.WhatsApp });
        var execution = new AiResponseExecution { Id = Guid.NewGuid(), UserId = 1, ChatSessionId = sessionId };
        var otherSession = new ChatSession { Id = Guid.NewGuid(), UserId = 1, Channel = ChatSessionChannels.Web };
        var sessionWithoutCalls = new ChatSession { Id = Guid.NewGuid(), UserId = 1, Channel = ChatSessionChannels.Web };
        var otherExecution = new AiResponseExecution { Id = Guid.NewGuid(), UserId = 1, ChatSessionId = otherSession.Id };
        db.ChatSessions.AddRange(otherSession, sessionWithoutCalls);
        db.AiResponseExecutions.Add(execution);
        db.AiResponseExecutions.Add(otherExecution);
        db.AiProviderCalls.AddRange(
            new AiProviderCall { Id = Guid.NewGuid(), ResponseExecutionId = execution.Id, Model = "model-a", Purpose = "Answer", InputTokens = 120, OutputTokens = 30, InputCostUsd = 0.0012m, OutputCostUsd = 0.0006m },
            new AiProviderCall { Id = Guid.NewGuid(), ResponseExecutionId = execution.Id, Model = "model-a", Purpose = "Tool", InputTokens = 80, OutputTokens = 20, InputCostUsd = 0.0008m, OutputCostUsd = 0.0004m },
            new AiProviderCall { Id = Guid.NewGuid(), ResponseExecutionId = otherExecution.Id, Model = "model-a", Purpose = "Answer", InputTokens = 999, OutputTokens = 999, InputCostUsd = 9m, OutputCostUsd = 9m });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var usage = await service.GetSessionUsageAsync(sessionId, 1, default);

        Assert.NotNull(usage);
        Assert.Equal(200, usage.InputTokens);
        Assert.Equal(50, usage.OutputTokens);
        Assert.Equal(250, usage.TotalTokens);
        Assert.Equal(0.003m, usage.TotalCostUsd);
        Assert.Null(await service.GetSessionUsageAsync(sessionId, 2, default));

        var otherUsage = await service.GetSessionUsageAsync(otherSession.Id, 1, default);
        Assert.NotNull(otherUsage);
        Assert.Equal(1_998, otherUsage.TotalTokens);
        Assert.Equal(18m, otherUsage.TotalCostUsd);

        var emptyUsage = await service.GetSessionUsageAsync(sessionWithoutCalls.Id, 1, default);
        Assert.NotNull(emptyUsage);
        Assert.Equal(0, emptyUsage.TotalTokens);
        Assert.Equal(0m, emptyUsage.TotalCostUsd);
    }

    private static AiConsumptionService CreateService(ImportDbContext db) => new(db, Options.Create(new AssistantOptions { Model = "fallback" }));
    private static ImportDbContext CreateDb() => new(new DbContextOptionsBuilder<ImportDbContext>().UseInMemoryDatabase($"ai-consumption-{Guid.NewGuid()}").Options);
    private static AppUser User(long id) => new() { Id = id, Name = $"User {id}", Email = $"user{id}@test.com", PasswordHash = "hash", Role = AppUserRoles.Vendas };
}
