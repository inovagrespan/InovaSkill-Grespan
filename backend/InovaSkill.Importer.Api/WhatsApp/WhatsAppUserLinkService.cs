using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using InovaSkill.Importer.Application.WhatsApp;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.WhatsApp;

public sealed class WhatsAppUserLinkService(
    ImportDbContext db,
    IWhatsAppGateway gateway,
    IOptions<WhatsAppOptions> options)
{
    public const int VerificationCodeDigits = 6;
    private readonly WhatsAppOptions settings = options.Value;

    public Task<WhatsAppUserLink?> FindAsync(long userId, CancellationToken cancellationToken) =>
        db.WhatsAppUserLinks.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public async Task<WhatsAppUserLink> StartVerificationAsync(long userId, string phone, CancellationToken cancellationToken)
    {
        var normalized = NormalizePhone(phone);
        var ownedByAnotherUser = await db.WhatsAppUserLinks.AnyAsync(x => x.NormalizedPhone == normalized && x.UserId != userId, cancellationToken);
        if (ownedByAnotherUser) throw new InvalidOperationException("Este telefone já está vinculado a outro usuário.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var now = DateTime.UtcNow;
        var link = await db.WhatsAppUserLinks.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var wasCreated = link is null;
        var previous = link is null ? null : new LinkState(
            link.NormalizedPhone, link.Status, link.VerificationCodeHash, link.VerificationExpiresAt,
            link.VerificationAttempts, link.ConfirmedAt, link.UpdatedAt);
        if (link is null)
        {
            link = new WhatsAppUserLink { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now };
            db.WhatsAppUserLinks.Add(link);
        }
        link.NormalizedPhone = normalized;
        link.Status = WhatsAppUserLinkStatuses.Pending;
        link.VerificationCodeHash = HashCode(link.Id, code);
        link.VerificationExpiresAt = now.AddMinutes(settings.VerificationCodeLifetimeMinutes);
        link.VerificationAttempts = 0;
        link.ConfirmedAt = null;
        link.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await gateway.SendTextAsync(normalized, $"Seu código de verificação Grespan é {code}. Ele expira em {settings.VerificationCodeLifetimeMinutes} minutos.", cancellationToken);
        }
        catch
        {
            if (wasCreated) db.WhatsAppUserLinks.Remove(link);
            else if (previous is not null)
            {
                link.NormalizedPhone = previous.NormalizedPhone;
                link.Status = previous.Status;
                link.VerificationCodeHash = previous.VerificationCodeHash;
                link.VerificationExpiresAt = previous.VerificationExpiresAt;
                link.VerificationAttempts = previous.VerificationAttempts;
                link.ConfirmedAt = previous.ConfirmedAt;
                link.UpdatedAt = previous.UpdatedAt;
            }
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        return link;
    }

    public async Task<WhatsAppUserLink> ConfirmAsync(long userId, string code, CancellationToken cancellationToken)
    {
        var link = await db.WhatsAppUserLinks.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Solicite um código antes de confirmar.");
        if (link.Status != WhatsAppUserLinkStatuses.Pending || link.VerificationExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("O código expirou. Solicite um novo código.");
        if (link.VerificationAttempts >= settings.MaximumVerificationAttempts)
            throw new InvalidOperationException("O limite de tentativas foi atingido. Solicite um novo código.");

        link.VerificationAttempts++;
        if (string.IsNullOrWhiteSpace(code) || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(link.VerificationCodeHash ?? string.Empty),
            Encoding.UTF8.GetBytes(HashCode(link.Id, code.Trim()))))
        {
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Código de verificação inválido.");
        }

        link.Status = WhatsAppUserLinkStatuses.Active;
        link.VerificationCodeHash = null;
        link.VerificationExpiresAt = null;
        link.ConfirmedAt = DateTime.UtcNow;
        link.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return link;
    }

    public async Task RevokeAsync(long userId, CancellationToken cancellationToken)
    {
        var link = await db.WhatsAppUserLinks.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (link is null) return;
        link.Status = WhatsAppUserLinkStatuses.Revoked;
        link.VerificationCodeHash = null;
        link.VerificationExpiresAt = null;
        link.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static string NormalizePhone(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15) throw new ArgumentException("Informe um telefone válido com DDD e código do país.");
        if (digits.Length is 10 or 11) digits = "55" + digits;
        return "+" + digits;
    }

    public static string MaskPhone(string phone) => phone.Length < 7 ? "***" : $"{phone[..3]}*****{phone[^4..]}";
    private static string HashCode(Guid linkId, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{linkId:N}:{code}")));
    private sealed record LinkState(
        string NormalizedPhone, string Status, string? VerificationCodeHash,
        DateTime? VerificationExpiresAt, int VerificationAttempts, DateTime? ConfirmedAt, DateTime UpdatedAt);
}
