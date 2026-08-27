using System.Security.Claims;
using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(
    BusinessAssistantService assistantService,
    IChatHistoryStore historyStore,
    IOptions<AssistantOptions> options,
    AiConsumptionService? consumptionService = null) : ControllerBase
{
    private const int MaximumConversationHistoryMessages = 1000;
    private const int ConversationHistoryPageSize = 20;

    [HttpPost("ask")]
    public async Task<ActionResult<AssistantAnswerResponse>> Ask(
        AssistantQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var question = (request.Message ?? request.Question)?.Trim() ?? string.Empty;
        if (question.Length == 0)
        {
            return BadRequest(new ProblemDetails { Detail = "Informe uma pergunta." });
        }
        if (question.Length > options.Value.MaximumQuestionLength)
        {
            return BadRequest(new ProblemDetails
            {
                Detail = $"A pergunta deve ter no máximo {options.Value.MaximumQuestionLength} caracteres."
            });
        }

        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? string.Empty;
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new ProblemDetails { Detail = "Usuário autenticado inválido." });
        }

        var admission = consumptionService is null
            ? new AiUsageAdmission(true, 0, 0)
            : await consumptionService.BeginAsync(userId, role, cancellationToken);
        if (!admission.Allowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Detail = "Seu limite mensal de uso do assistente foi atingido. Procure um administrador para ajustar o limite."
            });
        }

        var succeeded = false;
        try
        {
            var answer = await assistantService.AnswerAsync(
                request.SessionId, question, new ChatExecutionContext(userId, role), cancellationToken);
            succeeded = true;
            return Ok(answer);
        }
        finally
        {
            if (consumptionService is not null)
                await consumptionService.CompleteAsync(succeeded, CancellationToken.None);
        }
    }

    [HttpPost("whatsapp-simulator")]
    public async Task<ActionResult<AssistantAnswerResponse>> SimulateWhatsApp(
        AssistantQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var question = (request.Message ?? request.Question)?.Trim() ?? string.Empty;
        if (question.Length == 0) return BadRequest(new ProblemDetails { Detail = "Digite uma mensagem." });
        if (question.Length > options.Value.MaximumQuestionLength)
            return BadRequest(new ProblemDetails { Detail = $"A mensagem deve ter no máximo {options.Value.MaximumQuestionLength} caracteres." });

        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? string.Empty;
        if (!TryGetUserId(out var userId)) return Unauthorized(new ProblemDetails { Detail = "Usuário autenticado inválido." });
        var admission = consumptionService is null
            ? new AiUsageAdmission(true, 0, 0)
            : await consumptionService.BeginAsync(userId, role, cancellationToken);
        if (!admission.Allowed)
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails { Detail = "Seu limite mensal de uso do assistente foi atingido." });

        var succeeded = false;
        try
        {
            var answer = await assistantService.AnswerAsync(
                request.SessionId, question, new ChatExecutionContext(userId, role), cancellationToken,
                ChatSessionChannels.WhatsApp);
            succeeded = true;
            return Ok(answer);
        }
        finally
        {
            if (consumptionService is not null) await consumptionService.CompleteAsync(succeeded, CancellationToken.None);
        }
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<AssistantConversationPageResponse>> ListSessions(
        CancellationToken cancellationToken,
        [FromQuery] int offset = 0)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var sessions = await historyStore.ListAsync(
            userId,
            Math.Max(0, offset),
            ConversationHistoryPageSize + 1,
            cancellationToken);
        var hasMore = sessions.Count > ConversationHistoryPageSize;
        var items = sessions.Take(ConversationHistoryPageSize).Select(session => new AssistantConversationSummaryResponse(
            session.SessionId,
            session.Preview,
            session.UpdatedAt)).ToList();
        return Ok(new AssistantConversationPageResponse(items, hasMore, Math.Max(0, offset) + items.Count));
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<ActionResult<AssistantConversationResponse>> GetSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var session = await historyStore.LoadAsync(
            sessionId,
            userId,
            MaximumConversationHistoryMessages,
            cancellationToken);
        if (session is null) return NotFound();

        return Ok(new AssistantConversationResponse(
            session.SessionId,
            session.Messages.Select(message => new AssistantConversationMessageResponse(
                message.Id,
                message.Role,
                message.Content,
                message.CreatedAt)).ToList()));
    }

    [HttpGet("sessions/{sessionId:guid}/usage")]
    public async Task<ActionResult<AssistantSessionUsageResponse>> GetSessionUsage(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (consumptionService is null) return Ok(new AssistantSessionUsageResponse(0, 0, 0, 0, 0, 0));

        var usage = await consumptionService.GetSessionUsageAsync(sessionId, userId, cancellationToken);
        if (usage is null) return NotFound();
        return Ok(new AssistantSessionUsageResponse(
            usage.InputTokens,
            usage.OutputTokens,
            usage.TotalTokens,
            usage.InputCostUsd,
            usage.OutputCostUsd,
            usage.TotalCostUsd));
    }

    private bool TryGetUserId(out long userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(userIdValue, out userId);
    }
}
