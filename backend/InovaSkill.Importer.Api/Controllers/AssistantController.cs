using System.Security.Claims;
using InovaSkill.Importer.Api.Assistant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(
    BusinessAssistantService assistantService,
    IOptions<AssistantOptions> options) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<ActionResult<AssistantAnswerResponse>> Ask(
        AssistantQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var question = request.Question?.Trim() ?? string.Empty;
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
        return Ok(await assistantService.AnswerAsync(question, role, cancellationToken));
    }
}
