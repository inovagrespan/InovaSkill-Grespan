using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/admin/ai-consumption")]
public sealed class AiConsumptionAdminController(ImportDbContext db, AiConsumptionService consumptionService) : ControllerBase
{
    private const int MaximumDetailRows = 500;

    [HttpGet("report")]
    public async Task<ActionResult> Report([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] long? userId, CancellationToken cancellationToken)
    {
        var start = from?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-30);
        var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
        if (end <= start) return BadRequest(new ProblemDetails { Detail = "O período informado é inválido." });
        var calls = db.AiProviderCalls.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < end);
        if (userId.HasValue) calls = calls.Where(x => x.ResponseExecution.UserId == userId.Value);
        var total = await calls.GroupBy(_ => 1).Select(group => new
        {
            inputTokens = group.Sum(x => (long)x.InputTokens), outputTokens = group.Sum(x => (long)x.OutputTokens),
            totalTokens = group.Sum(x => (long)x.InputTokens + x.OutputTokens),
            estimatedCostUsd = group.Sum(x => x.InputCostUsd + x.OutputCostUsd), calls = group.Count(),
            responses = group.Select(x => x.ResponseExecutionId).Distinct().Count()
        }).SingleOrDefaultAsync(cancellationToken);
        var byUser = await calls.GroupBy(x => new { x.ResponseExecution.UserId, x.ResponseExecution.User.Name })
            .Select(group => new { userId = group.Key.UserId, userName = group.Key.Name, totalTokens = group.Sum(x => (long)x.InputTokens + x.OutputTokens), estimatedCostUsd = group.Sum(x => x.InputCostUsd + x.OutputCostUsd), calls = group.Count() })
            .OrderByDescending(x => x.totalTokens).ToListAsync(cancellationToken);
        var details = await calls.OrderByDescending(x => x.CreatedAt).Take(MaximumDetailRows)
            .Select(x => new { x.Id, x.ResponseExecutionId, userId = x.ResponseExecution.UserId, userName = x.ResponseExecution.User.Name, x.Model, x.Purpose, x.Status, x.InputTokens, x.OutputTokens, totalTokens = x.InputTokens + x.OutputTokens, estimatedCostUsd = x.InputCostUsd + x.OutputCostUsd, x.CreatedAt })
            .ToListAsync(cancellationToken);
        return Ok(new { from = start, to = end, total = total ?? new { inputTokens = 0L, outputTokens = 0L, totalTokens = 0L, estimatedCostUsd = 0m, calls = 0, responses = 0 }, byUser, details });
    }

    [HttpGet("configuration")]
    public async Task<ActionResult> Configuration(CancellationToken cancellationToken)
    {
        var settings = await consumptionService.GetSettingsAsync(cancellationToken);
        var prices = await db.AiModelPrices.AsNoTracking().OrderByDescending(x => x.EffectiveFrom).ToListAsync(cancellationToken);
        var users = await db.AppUsers.AsNoTracking().OrderBy(x => x.Name)
            .GroupJoin(db.AiUserLimits.AsNoTracking(), user => user.Id, limit => limit.UserId, (user, limits) => new { user.Id, user.Name, user.Email, user.Role, limit = limits.FirstOrDefault() })
            .Select(x => new { userId = x.Id, x.Name, x.Email, x.Role, monthlyTokenLimit = x.limit == null ? null : x.limit.MonthlyTokenLimit, alertPercentage = x.limit == null ? null : x.limit.AlertPercentage }).ToListAsync(cancellationToken);
        return Ok(new { settings.Model, settings.DefaultMonthlyTokenLimit, settings.DefaultAlertPercentage, prices, users });
    }

    [HttpPut("configuration")]
    public async Task<ActionResult> UpdateConfiguration(UpdateAiSettingsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Model) || request.DefaultMonthlyTokenLimit < 0 || request.DefaultAlertPercentage is <= 0 or > 100)
            return BadRequest(new ProblemDetails { Detail = "Informe modelo, limite e percentual válidos." });
        var settings = await consumptionService.GetSettingsAsync(cancellationToken);
        settings.Model = request.Model.Trim(); settings.DefaultMonthlyTokenLimit = request.DefaultMonthlyTokenLimit;
        settings.DefaultAlertPercentage = request.DefaultAlertPercentage; settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPut("users/{userId:long}/limit")]
    public async Task<ActionResult> UpdateUserLimit(long userId, UpdateAiUserLimitRequest request, CancellationToken cancellationToken)
    {
        if (request.MonthlyTokenLimit < 0 || request.AlertPercentage is <= 0 or > 100)
            return BadRequest(new ProblemDetails { Detail = "Limite ou percentual inválido." });
        if (!await db.AppUsers.AnyAsync(x => x.Id == userId, cancellationToken)) return NotFound();
        var limit = await db.AiUserLimits.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (request.MonthlyTokenLimit is null && request.AlertPercentage is null)
        {
            if (limit is not null) db.AiUserLimits.Remove(limit);
        }
        else if (limit is null)
        {
            db.AiUserLimits.Add(new AiUserLimit { UserId = userId, MonthlyTokenLimit = request.MonthlyTokenLimit, AlertPercentage = request.AlertPercentage });
        }
        else { limit.MonthlyTokenLimit = request.MonthlyTokenLimit; limit.AlertPercentage = request.AlertPercentage; limit.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPost("prices")]
    public async Task<ActionResult> AddPrice(CreateAiModelPriceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Model) || request.InputPricePerMillionUsd < 0 || request.OutputPricePerMillionUsd < 0)
            return BadRequest(new ProblemDetails { Detail = "Informe modelo e preços válidos." });
        var price = new AiModelPrice { Model = request.Model.Trim(), InputPricePerMillionUsd = request.InputPricePerMillionUsd, OutputPricePerMillionUsd = request.OutputPricePerMillionUsd, EffectiveFrom = request.EffectiveFrom.ToUniversalTime() };
        db.AiModelPrices.Add(price); await db.SaveChangesAsync(cancellationToken); return Created(string.Empty, price);
    }

    [HttpGet("alerts")]
    public async Task<ActionResult> Alerts(CancellationToken cancellationToken) => Ok(await db.AiConsumptionAlerts.AsNoTracking().OrderBy(x => x.ReadAt != null).ThenByDescending(x => x.CreatedAt)
        .Select(x => new { x.Id, x.UserId, userName = x.User.Name, x.PeriodMonth, x.Level, x.ConsumedTokens, x.TokenLimit, x.CreatedAt, x.ReadAt }).ToListAsync(cancellationToken));

    [HttpPut("alerts/{id:guid}/read")]
    public async Task<ActionResult> ReadAlert(Guid id, CancellationToken cancellationToken)
    {
        var changed = await db.AiConsumptionAlerts.Where(x => x.Id == id && x.ReadAt == null).ExecuteUpdateAsync(x => x.SetProperty(p => p.ReadAt, DateTime.UtcNow), cancellationToken);
        return changed == 0 ? NotFound() : NoContent();
    }
}

public sealed record UpdateAiSettingsRequest(string Model, long DefaultMonthlyTokenLimit, decimal DefaultAlertPercentage);
public sealed record UpdateAiUserLimitRequest(long? MonthlyTokenLimit, decimal? AlertPercentage);
public sealed record CreateAiModelPriceRequest(string Model, decimal InputPricePerMillionUsd, decimal OutputPricePerMillionUsd, DateTime EffectiveFrom);
