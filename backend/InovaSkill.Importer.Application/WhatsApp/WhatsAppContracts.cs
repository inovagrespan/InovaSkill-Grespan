namespace InovaSkill.Importer.Application.WhatsApp;

public sealed record WhatsAppGatewayConnection(string Status, string? Phone);
public sealed record WhatsAppGatewayQrCode(string DataUrl);
public sealed record WhatsAppGatewaySendResult(string ProviderMessageId);

public interface IWhatsAppGateway
{
    Task<WhatsAppGatewayConnection> GetConnectionAsync(CancellationToken cancellationToken);
    Task<WhatsAppGatewayConnection> StartConnectionAsync(CancellationToken cancellationToken);
    Task<WhatsAppGatewayQrCode?> GetQrCodeAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<WhatsAppGatewaySendResult> SendTextAsync(string normalizedPhone, string text, CancellationToken cancellationToken);
    Task<Stream> DownloadMediaAsync(string providerMessageId, CancellationToken cancellationToken);
}

public interface IAudioTranscriptionService
{
    Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken cancellationToken);
}

public interface IWhatsAppMessageQueue
{
    Task<Guid?> TryQueueAsync(Guid receiptId, CancellationToken cancellationToken);
}

public static class WhatsAppJobCodes
{
    public const string MessageProcessing = "WHATSAPP_MESSAGE_PROCESSING";
}
