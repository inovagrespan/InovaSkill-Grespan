using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Api.WhatsApp;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.WhatsApp;
using InovaSkill.Importer.Application.WhatsApp;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

    private static ImportDbContext CreateDatabase() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static WhatsAppUserLinkService CreateService(ImportDbContext db, IWhatsAppGateway gateway) =>
        new(db, gateway, Options.Create(new WhatsAppOptions { VerificationCodeLifetimeMinutes = 10, MaximumVerificationAttempts = 5 }));

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
}
