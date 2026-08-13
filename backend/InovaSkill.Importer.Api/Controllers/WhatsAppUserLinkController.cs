using System.Security.Claims;
using InovaSkill.Importer.Api.WhatsApp;
using Microsoft.AspNetCore.Mvc;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/whatsapp/user-link")]
public sealed class WhatsAppUserLinkController(WhatsAppUserLinkService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WhatsAppUserLinkResponse>> Get(CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var link = await service.FindAsync(userId, cancellationToken);
        return Ok(link is null
            ? new WhatsAppUserLinkResponse(null, "not_configured", null, null)
            : new WhatsAppUserLinkResponse(link.Id, link.Status, WhatsAppUserLinkService.MaskPhone(link.NormalizedPhone), link.ConfirmedAt));
    }

    [HttpPost("verification")]
    public async Task<ActionResult<WhatsAppUserLinkResponse>> Verify(WhatsAppVerificationRequest request, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        try
        {
            var link = await service.StartVerificationAsync(userId, request.Phone, cancellationToken);
            return Ok(new WhatsAppUserLinkResponse(link.Id, link.Status, WhatsAppUserLinkService.MaskPhone(link.NormalizedPhone), null));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new ProblemDetails { Detail = exception.Message });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "WhatsApp indisponível",
                Detail = "O número não foi vinculado porque o conector local do WhatsApp não está rodando. Use o Simulador WhatsApp ou peça ao administrador para iniciar o whatsapp-bridge.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }

    [HttpPost("confirmation")]
    public async Task<ActionResult<WhatsAppUserLinkResponse>> Confirm(WhatsAppConfirmationRequest request, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        try
        {
            var link = await service.ConfirmAsync(userId, request.Code, cancellationToken);
            return Ok(new WhatsAppUserLinkResponse(link.Id, link.Status, WhatsAppUserLinkService.MaskPhone(link.NormalizedPhone), link.ConfirmedAt));
        }
        catch (InvalidOperationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        await service.RevokeAsync(userId, cancellationToken);
        return NoContent();
    }

    private bool TryUserId(out long userId) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out userId);
}

public sealed record WhatsAppVerificationRequest(string Phone);
public sealed record WhatsAppConfirmationRequest(string Code);
public sealed record WhatsAppUserLinkResponse(Guid? Id, string Status, string? MaskedPhone, DateTime? ConfirmedAt);
