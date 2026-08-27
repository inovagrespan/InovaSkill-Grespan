using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Assistant;

public sealed class AiConsumptionService(
    ImportDbContext db,
    IOptions<AssistantOptions> options)
{
    public const long DefaultMonthlyTokenLimit = 1_000_000;
    public const decimal DefaultAlertPercentage = 80m;
    private const decimal TokensPerMillion = 1_000_000m;
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();
    private readonly AssistantOptions assistantOptions = options.Value;
    private Guid? currentExecutionId;

    public async Task<string> GetModelAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(settings.Model) ? assistantOptions.Model : settings.Model;
    }

    public async Task<AiUsageAdmission> BeginAsync(long userId, string role, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var userLimit = await db.AiUserLimits.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var limit = userLimit?.MonthlyTokenLimit ?? settings.DefaultMonthlyTokenLimit;
        var (startUtc, endUtc, _) = CurrentPeriod();
        var consumed = await db.AiProviderCalls
            .Where(x => x.ResponseExecution.UserId == userId && x.CreatedAt >= startUtc && x.CreatedAt < endUtc)
            .SumAsync(x => (long)x.InputTokens + x.OutputTokens, cancellationToken);
        if (role != AppUserRoles.AdminSystem && consumed >= limit)
        {
            return new AiUsageAdmission(false, consumed, limit);
        }

        var execution = new AiResponseExecution { Id = Guid.NewGuid(), UserId = userId };
        db.AiResponseExecutions.Add(execution);
        await db.SaveChangesAsync(cancellationToken);
        currentExecutionId = execution.Id;
        return new AiUsageAdmission(true, consumed, limit);
    }

    public async Task SetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (currentExecutionId is null) return;
        await db.AiResponseExecutions.Where(x => x.Id == currentExecutionId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ChatSessionId, sessionId), cancellationToken);
    }

    public async Task RecordCallAsync(
        string model, ChatModelRequestPurpose purpose, string status, string responseId,
        int inputTokens, int outputTokens, CancellationToken cancellationToken)
    {
        if (currentExecutionId is null) return;
        var now = DateTime.UtcNow;
        var price = await db.AiModelPrices.AsNoTracking()
            .Where(x => x.Model == model && x.EffectiveFrom <= now)
            .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(cancellationToken);
        var inputPrice = price?.InputPricePerMillionUsd ?? 0m;
        var outputPrice = price?.OutputPricePerMillionUsd ?? 0m;
        db.AiProviderCalls.Add(new AiProviderCall
        {
            Id = Guid.NewGuid(), ResponseExecutionId = currentExecutionId.Value,
            ProviderResponseId = responseId, Model = model, Purpose = purpose.ToString(), Status = status,
            InputTokens = inputTokens, OutputTokens = outputTokens,
            InputPricePerMillionUsd = inputPrice, OutputPricePerMillionUsd = outputPrice,
            InputCostUsd = inputTokens * inputPrice / TokensPerMillion,
            OutputCostUsd = outputTokens * outputPrice / TokensPerMillion,
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(bool succeeded, CancellationToken cancellationToken)
    {
        if (currentExecutionId is null) return;
        var executionId = currentExecutionId.Value;
        currentExecutionId = null;
        await db.AiResponseExecutions.Where(x => x.Id == executionId).ExecuteUpdateAsync(
            x => x.SetProperty(p => p.Status, succeeded ? AiConsumptionStatuses.Completed : AiConsumptionStatuses.Failed)
                .SetProperty(p => p.CompletedAt, DateTime.UtcNow), cancellationToken);

        var execution = await db.AiResponseExecutions.AsNoTracking().SingleAsync(x => x.Id == executionId, cancellationToken);
        await CreateAlertsAsync(execution.UserId, cancellationToken);
    }

    public async Task<AiSessionUsage?> GetSessionUsageAsync(
        Guid sessionId,
        long userId,
        CancellationToken cancellationToken)
    {
        var ownsSession = await db.ChatSessions.AsNoTracking()
            .AnyAsync(x => x.Id == sessionId && x.UserId == userId, cancellationToken);
        if (!ownsSession) return null;

        var calls = await db.AiProviderCalls.AsNoTracking()
            .Where(x => x.ResponseExecution.ChatSessionId == sessionId && x.ResponseExecution.UserId == userId)
            .Select(x => new { x.InputTokens, x.OutputTokens, x.InputCostUsd, x.OutputCostUsd })
            .ToListAsync(cancellationToken);
        var inputTokens = calls.Sum(x => (long)x.InputTokens);
        var outputTokens = calls.Sum(x => (long)x.OutputTokens);
        var inputCostUsd = calls.Sum(x => x.InputCostUsd);
        var outputCostUsd = calls.Sum(x => x.OutputCostUsd);
        return new AiSessionUsage(
            inputTokens,
            outputTokens,
            inputTokens + outputTokens,
            inputCostUsd,
            outputCostUsd,
            inputCostUsd + outputCostUsd);
    }

    public async Task<AiConsumptionSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.AiConsumptionSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (settings is not null) return settings;
        settings = new AiConsumptionSettings
        {
            Id = 1, Model = assistantOptions.Model,
            DefaultMonthlyTokenLimit = DefaultMonthlyTokenLimit,
            DefaultAlertPercentage = DefaultAlertPercentage
        };
        db.AiConsumptionSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task CreateAlertsAsync(long userId, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var custom = await db.AiUserLimits.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var limit = custom?.MonthlyTokenLimit ?? settings.DefaultMonthlyTokenLimit;
        if (limit <= 0) return;
        var threshold = custom?.AlertPercentage ?? settings.DefaultAlertPercentage;
        var (startUtc, endUtc, period) = CurrentPeriod();
        var consumed = await db.AiProviderCalls.Where(x => x.ResponseExecution.UserId == userId && x.CreatedAt >= startUtc && x.CreatedAt < endUtc)
            .SumAsync(x => (long)x.InputTokens + x.OutputTokens, cancellationToken);
        var level = consumed >= limit ? AiConsumptionAlertLevels.LimitReached
            : consumed * 100m / limit >= threshold ? AiConsumptionAlertLevels.Warning : null;
        if (level is null) return;
        var exists = await db.AiConsumptionAlerts.AnyAsync(x => x.UserId == userId && x.PeriodMonth == period && x.Level == level, cancellationToken);
        if (!exists)
        {
            db.AiConsumptionAlerts.Add(new AiConsumptionAlert
            {
                Id = Guid.NewGuid(), UserId = userId, PeriodMonth = period, Level = level,
                ConsumedTokens = consumed, TokenLimit = limit
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static (DateTime StartUtc, DateTime EndUtc, DateOnly Period) CurrentPeriod()
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessTimeZone);
        var startLocal = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddMonths(1);
        return (TimeZoneInfo.ConvertTimeToUtc(startLocal, BusinessTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(endLocal, BusinessTimeZone), DateOnly.FromDateTime(startLocal));
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }
}

public sealed record AiUsageAdmission(bool Allowed, long ConsumedTokens, long TokenLimit);

public sealed record AiSessionUsage(
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal InputCostUsd,
    decimal OutputCostUsd,
    decimal TotalCostUsd);
