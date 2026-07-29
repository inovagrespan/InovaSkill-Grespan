using System.Security.Claims;
using InovaSkill.Importer.Api.Assistant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(
    BusinessAssistantService assistantService,
    IChatHistoryStore historyStore,
    IOptions<AssistantOptions> options) : ControllerBase
{
    private const int MaximumConversationHistoryMessages = 100;
    private const int MaximumConversationHistorySessions = 20;

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

        return Ok(await assistantService.AnswerAsync(
            request.SessionId,
            question,
            new ChatExecutionContext(userId, role),
            cancellationToken));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<AssistantConversationSummaryResponse>>> ListSessions(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var sessions = await historyStore.ListAsync(
            userId,
            MaximumConversationHistorySessions,
            cancellationToken);
        return Ok(sessions.Select(session => new AssistantConversationSummaryResponse(
            session.SessionId,
            session.Preview,
            session.UpdatedAt)));
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

    private bool TryGetUserId(out long userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(userIdValue, out userId);
    }
}
