using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Api.WhatsApp;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.WhatsApp;
using InovaSkill.Importer.Application.WhatsApp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace InovaSkill.Importer.Tests.Api;

public sealed class WhatsAppIntegrationTests
{
    [Theory]
    [InlineData("(11) 99999-9999", "+5511999999999")]
    [InlineData("+55 11 99999-9999", "+5511999999999")]
    [InlineData("5511999999999", "+5511999999999")]
    public void NormalizePhone_ProducesE164(string input, string expected) =>
        Assert.Equal(expected, WhatsAppUserLinkService.NormalizePhone(input));

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("1234567890123456")]
    public void NormalizePhone_RejectsInvalidValues(string input) =>
        Assert.Throws<ArgumentException>(() => WhatsAppUserLinkService.NormalizePhone(input));

    [Fact]
    public void NormalizeRemoteJid_RejectsGroupAndKeepsPhoneIdentity()
    {
        Assert.Equal("+5511999999999", WhatsAppWebhookController.NormalizeRemoteJid("5511999999999@s.whatsapp.net"));
        Assert.Throws<ArgumentException>(() => WhatsAppWebhookController.NormalizeRemoteJid("invalid@s.whatsapp.net"));
    }

    [Theory]
    [InlineData("admin_system", true)]
    [InlineData("admin", false)]
    [InlineData("vendas", false)]
    public void AdminConnection_IsRestrictedToSystemAdministrator(string role, bool expected) =>
        Assert.Equal(expected, ApiAccessPolicy.CanAccess(role, "/api/admin/whatsapp/connection", HttpMethods.Get));

    [Fact]
    public void PersistenceModel_UsesRequiredUniqueIndexesAndSeparateChannel()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>().UseNpgsql("Host=localhost;Database=model").Options;
        using var db = new ImportDbContext(options);
        var link = db.Model.FindEntityType(typeof(WhatsAppUserLink))!;
        Assert.Contains(link.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(WhatsAppUserLink.UserId));
        Assert.Contains(link.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(WhatsAppUserLink.NormalizedPhone));
        var receipt = db.Model.FindEntityType(typeof(WhatsAppMessageReceipt))!;
        Assert.Contains(receipt.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(WhatsAppMessageReceipt.ProviderMessageId));
        Assert.Equal(ChatSessionChannels.Web, new ChatSession().Channel);
    }

    [Fact]
    public async Task Verification_ActivatesOnlyWithCodeSentToThePhone()
    {
        await using var db = CreateDatabase();
        db.AppUsers.Add(new AppUser { Id = 7, Name = "Usuário", Email = "user@test", PasswordHash = "hash", Role = AppUserRoles.Vendas });
        await db.SaveChangesAsync();
        var gateway = new CapturingGateway();
        var service = CreateService(db, gateway);

        var pending = await service.StartVerificationAsync(7, "11999999999", CancellationToken.None);
        Assert.Equal(WhatsAppUserLinkStatuses.Pending, pending.Status);
        var code = new string(gateway.LastText!.Where(char.IsDigit).Take(6).ToArray());
        var active = await service.ConfirmAsync(7, code, CancellationToken.None);

        Assert.Equal(WhatsAppUserLinkStatuses.Active, active.Status);
        Assert.Null(active.VerificationCodeHash);
        Assert.NotNull(active.ConfirmedAt);
    }

    [Fact]
    public async Task Verification_InvalidCodeIncrementsAttemptsAndDoesNotActivate()
    {
        await using var db = CreateDatabase();
        db.AppUsers.Add(new AppUser { Id = 8, Name = "Usuário", Email = "other@test", PasswordHash = "hash", Role = AppUserRoles.Vendas });
        await db.SaveChangesAsync();
        var service = CreateService(db, new CapturingGateway());
        await service.StartVerificationAsync(8, "+5511888888888", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(8, "000000", CancellationToken.None));
        var stored = await db.WhatsAppUserLinks.SingleAsync();
        Assert.Equal(1, stored.VerificationAttempts);
        Assert.Equal(WhatsAppUserLinkStatuses.Pending, stored.Status);
    }

    [Fact]
    public async Task Verification_GatewayFailureDoesNotLeavePendingLink()
    {
        await using var db = CreateDatabase();
        db.AppUsers.Add(new AppUser { Id = 9, Name = "Usuário", Email = "failure@test", PasswordHash = "hash", Role = AppUserRoles.Vendas });
        await db.SaveChangesAsync();
        var service = CreateService(db, new FailingGateway());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.StartVerificationAsync(9, "+5511777777777", CancellationToken.None));

        Assert.Empty(db.WhatsAppUserLinks);
    }

    [Fact]
    public async Task Webhook_QueuesSingleNoticeAndDoesNotQueueMessagesDuringFloodCooldown()
    {
        await using var db = CreateDatabase();
        var link = new WhatsAppUserLink
        {
            Id = Guid.NewGuid(), UserId = 12, NormalizedPhone = "+5511999999999",
            Status = WhatsAppUserLinkStatuses.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.AppUsers.Add(new AppUser { Id = 12, Name = "Flood", Email = "flood@test", PasswordHash = "hash", Role = AppUserRoles.Vendas });
        db.WhatsAppUserLinks.Add(link);
        for (var index = 0; index < 8; index++)
        {
            db.WhatsAppMessageReceipts.Add(new WhatsAppMessageReceipt
            {
                Id = Guid.NewGuid(), ProviderMessageId = $"existing-{index}", WhatsAppUserLinkId = link.Id,
                Direction = WhatsAppMessageDirections.Inbound, MessageType = "text",
                Status = WhatsAppMessageStatuses.Completed, TextContent = "mensagem",
                CreatedAt = DateTime.UtcNow.AddSeconds(-index), UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        var queue = new CapturingQueue();
        var controller = CreateWebhookController(db, queue);

        await controller.Receive(WebhookPayload("blocked-1"), default);
        await controller.Receive(WebhookPayload("blocked-2"), default);

        Assert.Single(queue.ReceiptIds);
        Assert.Equal(WhatsAppMessageStatuses.RateLimitNotice,
            (await db.WhatsAppMessageReceipts.SingleAsync(x => x.ProviderMessageId == "blocked-1")).Status);
        Assert.Equal(WhatsAppMessageStatuses.RateLimited,
            (await db.WhatsAppMessageReceipts.SingleAsync(x => x.ProviderMessageId == "blocked-2")).Status);
        Assert.NotNull((await db.WhatsAppUserLinks.SingleAsync()).FloodBlockedUntil);
    }

    private static ImportDbContext CreateDatabase() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static WhatsAppUserLinkService CreateService(ImportDbContext db, IWhatsAppGateway gateway) =>
        new(db, gateway, Options.Create(new WhatsAppOptions { VerificationCodeLifetimeMinutes = 10, MaximumVerificationAttempts = 5 }));
    private static WhatsAppWebhookController CreateWebhookController(ImportDbContext db, IWhatsAppMessageQueue queue)
    {
        var controller = new WhatsAppWebhookController(db, queue, Options.Create(new WhatsAppOptions
        {
            WebhookSecret = "test-secret", FloodMaximumMessages = 8, FloodWindowSeconds = 30, FloodCooldownSeconds = 30
        }));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Headers["X-Webhook-Secret"] = "test-secret";
        return controller;
    }

    private static JsonElement WebhookPayload(string providerId) => JsonSerializer.SerializeToElement(new
    {
        @event = "messages.upsert",
        data = new
        {
            key = new { fromMe = false, remoteJid = "5511999999999@s.whatsapp.net", id = providerId },
            message = new { conversation = "mensagem" }
        }
    });

    private sealed class CapturingGateway : IWhatsAppGateway
    {
        public string? LastText { get; private set; }
        public Task<WhatsAppGatewaySendResult> SendTextAsync(string normalizedPhone, string text, CancellationToken cancellationToken)
        { LastText = text; return Task.FromResult(new WhatsAppGatewaySendResult("sent")); }
        public Task<WhatsAppGatewayConnection> GetConnectionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppGatewayConnection> StartConnectionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppGatewayQrCode?> GetQrCodeAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DisconnectAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> DownloadMediaAsync(string providerMessageId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FailingGateway : IWhatsAppGateway
    {
        public Task<WhatsAppGatewaySendResult> SendTextAsync(string normalizedPhone, string text, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
        public Task<WhatsAppGatewayConnection> GetConnectionAsync(CancellationToken cancellationToken) => throw new HttpRequestException();
        public Task<WhatsAppGatewayConnection> StartConnectionAsync(CancellationToken cancellationToken) => throw new HttpRequestException();
        public Task<WhatsAppGatewayQrCode?> GetQrCodeAsync(CancellationToken cancellationToken) => throw new HttpRequestException();
        public Task DisconnectAsync(CancellationToken cancellationToken) => throw new HttpRequestException();
        public Task<Stream> DownloadMediaAsync(string providerMessageId, CancellationToken cancellationToken) => throw new HttpRequestException();
    }

    private sealed class CapturingQueue : IWhatsAppMessageQueue
    {
        public List<Guid> ReceiptIds { get; } = [];
        public Task<Guid?> TryQueueAsync(Guid receiptId, CancellationToken cancellationToken)
        {
            ReceiptIds.Add(receiptId);
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }
}
