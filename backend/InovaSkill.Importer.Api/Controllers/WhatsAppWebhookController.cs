using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InovaSkill.Importer.Application.WhatsApp;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/integrations/whatsapp/webhook")]
public sealed class WhatsAppWebhookController(
    ImportDbContext db,
    IWhatsAppMessageQueue queue,
    IOptions<WhatsAppOptions> options) : ControllerBase
{
    private const long MaximumWebhookBytes = 256 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaximumWebhookBytes)]
    public async Task<IActionResult> Receive([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        if (!ValidSecret(Request.Headers["X-Webhook-Secret"].ToString(), options.Value.WebhookSecret)) return Unauthorized();
        var eventName = ReadString(payload, "event")?.ToLowerInvariant();
        if (eventName is "connection.update" or "connection_update")
        {
            await UpdateConnectionAsync(payload, cancellationToken);
            return Ok();
        }
        if (eventName is not "messages.upsert" and not "messages_upsert") return Ok();

        var data = payload.TryGetProperty("data", out var dataElement) ? dataElement : payload;
        if (!data.TryGetProperty("key", out var key)) return Ok();
        if (ReadBoolean(key, "fromMe") || ReadString(key, "remoteJid") is not { } remoteJid) return Ok();
        if (remoteJid.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase) || remoteJid.Contains("broadcast", StringComparison.OrdinalIgnoreCase)) return Ok();
        var providerId = ReadString(key, "id");
        if (string.IsNullOrWhiteSpace(providerId)) return BadRequest();

        string phone;
        try { phone = NormalizeRemoteJid(remoteJid); }
        catch (ArgumentException) { return Ok(); }
        var link = await db.WhatsAppUserLinks
            .SingleOrDefaultAsync(x => x.NormalizedPhone == phone && x.Status == WhatsAppUserLinkStatuses.Active, cancellationToken);
        if (link is null) return Ok();
        var existingReceiptId = await db.WhatsAppMessageReceipts.AsNoTracking()
            .Where(x => x.ProviderMessageId == providerId)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (existingReceiptId.HasValue)
        {
            await queue.TryQueueAsync(existingReceiptId.Value, cancellationToken);
            return Accepted();
        }

        if (!data.TryGetProperty("message", out var message)) return Ok();
        var text = ReadString(message, "conversation") ?? ReadNestedString(message, "extendedTextMessage", "text");
        var hasAudio = message.TryGetProperty("audioMessage", out _);
        if (string.IsNullOrWhiteSpace(text) && !hasAudio) return Ok();

        var now = DateTime.UtcNow;
        var floodWindowStart = now.AddSeconds(-Math.Max(1, options.Value.FloodWindowSeconds));
        var recentAcceptedMessages = await db.WhatsAppMessageReceipts.AsNoTracking().CountAsync(
            x => x.WhatsAppUserLinkId == link.Id &&
                 x.Direction == WhatsAppMessageDirections.Inbound &&
                 x.CreatedAt >= floodWindowStart &&
                 x.Status != WhatsAppMessageStatuses.RateLimited &&
                 x.Status != WhatsAppMessageStatuses.RateLimitNotice &&
                 x.Status != WhatsAppMessageStatuses.RateLimitNoticeSent,
            cancellationToken);
        var floodDecision = WhatsAppFloodPolicy.Evaluate(
            now,
            link.FloodBlockedUntil,
            recentAcceptedMessages,
            options.Value.FloodMaximumMessages,
            TimeSpan.FromSeconds(Math.Max(1, options.Value.FloodCooldownSeconds)));
        if (floodDecision.ShouldNotify)
        {
            if (db.Database.IsRelational())
            {
                var claimedNotice = await db.WhatsAppUserLinks
                    .Where(x => x.Id == link.Id && (x.FloodBlockedUntil == null || x.FloodBlockedUntil <= now))
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(x => x.FloodBlockedUntil, floodDecision.BlockedUntil),
                        cancellationToken);
                if (claimedNotice == 0) floodDecision = floodDecision with { ShouldNotify = false };
            }
            else
            {
                link.FloodBlockedUntil = floodDecision.BlockedUntil;
            }
        }
        var receipt = new WhatsAppMessageReceipt
        {
            Id = Guid.NewGuid(), ProviderMessageId = providerId, WhatsAppUserLinkId = link.Id,
            Direction = WhatsAppMessageDirections.Inbound, MessageType = hasAudio ? "audio" : "text",
            TextContent = text?.Trim(), MediaReference = hasAudio ? data.GetRawText() : null,
            Status = floodDecision.Allowed
                ? WhatsAppMessageStatuses.Received
                : floodDecision.ShouldNotify
                    ? WhatsAppMessageStatuses.RateLimitNotice
                    : WhatsAppMessageStatuses.RateLimited,
            CreatedAt = now, UpdatedAt = now
        };
        db.WhatsAppMessageReceipts.Add(receipt);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Ok(); }
        if (floodDecision.Allowed || floodDecision.ShouldNotify)
            await queue.TryQueueAsync(receipt.Id, cancellationToken);
        return Accepted();
    }

    private async Task UpdateConnectionAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var data = payload.TryGetProperty("data", out var item) ? item : payload;
        var state = ReadString(data, "state") ?? "disconnected";
        if (state.Equals("open", StringComparison.OrdinalIgnoreCase)) state = WhatsAppConnectionStatuses.Connected;
        var entity = await db.WhatsAppConnections.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (entity is null)
        {
            entity = new WhatsAppConnection { Id = 1, InstanceName = options.Value.InstanceName };
            db.WhatsAppConnections.Add(entity);
        }
        entity.Status = state.ToLowerInvariant();
        entity.LastEventAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static string NormalizeRemoteJid(string remoteJid)
    {
        var digits = new string(remoteJid.Split('@')[0].Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15) throw new ArgumentException("Identificador remoto inválido.");
        return "+" + digits;
    }

    private static bool ValidSecret(string actual, string expected) => !string.IsNullOrWhiteSpace(expected) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));
    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? ReadNestedString(JsonElement element, string parent, string child) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(parent, out var nested) ? ReadString(nested, child) : null;
    private static bool ReadBoolean(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
