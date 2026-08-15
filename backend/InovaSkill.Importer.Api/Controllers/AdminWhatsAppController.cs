using InovaSkill.Importer.Application.WhatsApp;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/admin/whatsapp/connection")]
public sealed class AdminWhatsAppController(
    IWhatsAppGateway gateway,
    ImportDbContext db,
    IOptions<WhatsAppOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WhatsAppConnectionResponse>> Get(CancellationToken cancellationToken)
    {
        try
        {
            var state = await gateway.GetConnectionAsync(cancellationToken);
            await SaveStateAsync(state, cancellationToken);
            return Ok(ToResponse(state));
        }
        catch (Exception exception) when (IsGatewayFailure(exception))
        {
            return Ok(Unavailable($"O conector local do WhatsApp não está rodando em {options.Value.BaseUrl}. Inicie-o com 'cd whatsapp-bridge && npm start'."));
        }
    }

    [HttpPost]
    public async Task<ActionResult<WhatsAppConnectionResponse>> Start(CancellationToken cancellationToken)
    {
        try
        {
            var state = await gateway.StartConnectionAsync(cancellationToken);
            await SaveStateAsync(state, cancellationToken);
            return Ok(ToResponse(state));
        }
        catch (Exception exception) when (IsGatewayFailure(exception))
        {
            return ProblemUnavailable($"Não foi possível acessar o conector local em {options.Value.BaseUrl}. Inicie-o com 'cd whatsapp-bridge && npm start'.");
        }
    }

    [HttpGet("qr-code")]
    public async Task<ActionResult<WhatsAppQrCodeResponse>> QrCode(CancellationToken cancellationToken)
    {
        try
        {
            var qr = await gateway.GetQrCodeAsync(cancellationToken);
            return qr is null ? NotFound(new ProblemDetails { Detail = "O QR Code ainda não foi gerado. Inicie a conexão e tente novamente." }) : Ok(new WhatsAppQrCodeResponse(qr.DataUrl));
        }
        catch (Exception exception) when (IsGatewayFailure(exception))
        {
            return ProblemUnavailable($"Não foi possível obter o QR Code porque o conector local não está acessível em {options.Value.BaseUrl}.");
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        try { await gateway.DisconnectAsync(cancellationToken); }
        catch (Exception exception) when (IsGatewayFailure(exception))
        { return ProblemUnavailable("Não foi possível desconectar porque o conector local está indisponível."); }
        await SaveStateAsync(new WhatsAppGatewayConnection(WhatsAppConnectionStatuses.Disconnected, null), cancellationToken);
        return NoContent();
    }

    private async Task SaveStateAsync(WhatsAppGatewayConnection state, CancellationToken cancellationToken)
    {
        var entity = await db.WhatsAppConnections.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (entity is null)
        {
            entity = new WhatsAppConnection { Id = 1, InstanceName = options.Value.InstanceName };
            db.WhatsAppConnections.Add(entity);
        }
        entity.Status = state.Status;
        entity.ConnectedPhone = state.Phone;
        entity.ConnectedAt = state.Status == WhatsAppConnectionStatuses.Connected ? entity.ConnectedAt ?? DateTime.UtcNow : null;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static WhatsAppConnectionResponse ToResponse(WhatsAppGatewayConnection state) =>
        new(state.Status, string.IsNullOrWhiteSpace(state.Phone) ? null : WhatsAppUserLinkMask.Mask(state.Phone), null, true);
    private WhatsAppConnectionResponse Unavailable(string detail) => new("unavailable", null, detail, false);
    private static bool IsGatewayFailure(Exception exception) => exception is HttpRequestException or TaskCanceledException;
    private ObjectResult ProblemUnavailable(string detail) => StatusCode(StatusCodes.Status503ServiceUnavailable,
        new ProblemDetails { Title = "Conector local indisponível", Detail = detail, Status = StatusCodes.Status503ServiceUnavailable });
}

internal static class WhatsAppUserLinkMask
{
    public static string Mask(string phone) => phone.Length < 7 ? "***" : $"{phone[..3]}*****{phone[^4..]}";
}

public sealed record WhatsAppConnectionResponse(string Status, string? MaskedPhone, string? Detail, bool ProviderAvailable);
public sealed record WhatsAppQrCodeResponse(string DataUrl);
