using System.Net.Http.Headers;
using System.Text.Json;
using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Application.WhatsApp;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.WhatsApp;

public sealed class OpenAiAudioTranscriptionService(
    IHttpClientFactory httpClientFactory,
    IOptions<AssistantOptions> assistantOptions,
    IOptions<WhatsAppOptions> whatsAppOptions) : IAudioTranscriptionService
{
    public async Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken cancellationToken)
    {
        if (audio.CanSeek && audio.Length > whatsAppOptions.Value.MaximumAudioBytes)
            throw new InvalidOperationException("O áudio excede o tamanho máximo permitido.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assistantOptions.Value.OpenAiApiKey);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("gpt-4o-mini-transcribe"), "model");
        content.Add(new StreamContent(audio), "file", fileName);
        request.Content = content;
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return document.RootElement.GetProperty("text").GetString()?.Trim()
            ?? throw new InvalidOperationException("A transcrição do áudio veio vazia.");
    }
}
