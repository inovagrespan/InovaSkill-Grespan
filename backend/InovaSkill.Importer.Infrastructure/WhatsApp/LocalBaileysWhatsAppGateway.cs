using System.Net.Http.Json;
using System.Text.Json;
using InovaSkill.Importer.Application.WhatsApp;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.WhatsApp;

public sealed class LocalBaileysWhatsAppGateway(
    IHttpClientFactory httpClientFactory,
    IOptions<WhatsAppOptions> options) : IWhatsAppGateway
{
    private readonly WhatsAppOptions settings = options.Value;

    public async Task<WhatsAppGatewayConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        using var document = await SendAsync(HttpMethod.Get, "connection", null, cancellationToken);
        var root = document.RootElement;
        var state = ReadString(root, "status") ?? "disconnected";
        var phone = ReadString(root, "phone");
        return new WhatsAppGatewayConnection(NormalizeState(state), NormalizeOptionalPhone(phone));
    }

    public async Task<WhatsAppGatewayConnection> StartConnectionAsync(CancellationToken cancellationToken)
    {
        using var document = await SendAsync(HttpMethod.Post, "connection/start", new { }, cancellationToken);
        return await GetConnectionOrConnectingAsync(cancellationToken);
    }

    public async Task<WhatsAppGatewayQrCode?> GetQrCodeAsync(CancellationToken cancellationToken)
    {
        using var document = await SendAsync(HttpMethod.Get, "connection/qr", null, cancellationToken);
        var base64 = ReadString(document.RootElement, "dataUrl");
        if (string.IsNullOrWhiteSpace(base64)) return null;
        return new WhatsAppGatewayQrCode(base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? base64 : $"data:image/png;base64,{base64}");
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken) =>
        (await SendAsync(HttpMethod.Delete, "connection", null, cancellationToken)).Dispose();

    public async Task<WhatsAppGatewaySendResult> SendTextAsync(string normalizedPhone, string text, CancellationToken cancellationToken)
    {
        using var document = await SendAsync(HttpMethod.Post, "messages/text", new
        {
            phone = normalizedPhone,
            text
        }, cancellationToken);
        var id = ReadString(document.RootElement, "id")
            ?? throw new InvalidOperationException("O bridge local não retornou o identificador da mensagem.");
        return new WhatsAppGatewaySendResult(id);
    }

    public async Task<Stream> DownloadMediaAsync(string providerMessageId, CancellationToken cancellationToken)
    {
        object message = new { key = new { id = providerMessageId } };
        if (providerMessageId.StartsWith('{'))
        {
            using var payload = JsonDocument.Parse(providerMessageId);
            message = payload.RootElement.Clone();
        }
        using var document = await SendAsync(HttpMethod.Post, "messages/media", new
        {
            message
        }, cancellationToken);
        var encoded = ReadString(document.RootElement, "base64")
            ?? throw new InvalidOperationException("O bridge local não retornou o áudio solicitado.");
        var comma = encoded.IndexOf(',');
        if (comma >= 0) encoded = encoded[(comma + 1)..];
        return new MemoryStream(Convert.FromBase64String(encoded));
    }

    private async Task<WhatsAppGatewayConnection> GetConnectionOrConnectingAsync(CancellationToken cancellationToken)
    {
        try { return await GetConnectionAsync(cancellationToken); }
        catch (HttpRequestException) { return new WhatsAppGatewayConnection("connecting", null); }
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.GatewayTimeoutSeconds));
        using var request = new HttpRequestMessage(method, new Uri(new Uri(settings.BaseUrl.TrimEnd('/') + "/"), path));
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await httpClientFactory.CreateClient().SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            var providerContent = await response.Content.ReadAsStringAsync(timeout.Token);
            var detail = TryReadDetail(providerContent) ?? $"Bridge local retornou HTTP {(int)response.StatusCode}.";
            throw new HttpRequestException(detail);
        }
        var content = await response.Content.ReadAsStringAsync(timeout.Token);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content);
    }

    private static string? TryReadDetail(string content)
    {
        try { using var document = JsonDocument.Parse(content); return ReadString(document.RootElement, "detail"); }
        catch (JsonException) { return null; }
    }
    private static string? ReadString(JsonElement root, params string[] path)
    {
        foreach (var name in path)
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out root)) return null;
        return root.ValueKind == JsonValueKind.String ? root.GetString() : null;
    }
    private static string NormalizeState(string state) => state.Equals("open", StringComparison.OrdinalIgnoreCase) ? "connected" : state.ToLowerInvariant();
    private static string? NormalizeOptionalPhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : "+" + new string(value.Where(char.IsDigit).ToArray());
}
