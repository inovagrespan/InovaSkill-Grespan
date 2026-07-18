using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Assistant;

public sealed class OpenAiChatModelClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AssistantOptions> options,
    ILogger<OpenAiChatModelClient> logger) : IChatModelClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly AssistantOptions assistantOptions = options.Value;

    public async Task<ChatModelResponse> SendAsync(ChatModelRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assistantOptions.OpenAiApiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY não foi configurada.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(assistantOptions.OpenAiTimeoutSeconds));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assistantOptions.OpenAiApiKey);
        httpRequest.Content = JsonContent.Create(new
        {
            model = request.Model,
            instructions = request.Instructions,
            previous_response_id = request.PreviousResponseId,
            input = request.Messages.Select(ToOpenAiInputItem),
            tools = request.Tools.Select(tool => new
            {
                type = "function",
                name = tool.Name,
                description = tool.Description,
                parameters = tool.Parameters,
                strict = true
            }),
            tool_choice = "auto"
        }, options: SerializerOptions);

        using var response = await httpClientFactory.CreateClient().SendAsync(httpRequest, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenAI retornou status {StatusCode} para o assistente.", response.StatusCode);
            throw new HttpRequestException("OpenAI indisponível.");
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(timeout.Token));
        var responseId = document.RootElement.GetProperty("id").GetString() ?? string.Empty;
        var outputText = ReadOutputText(document.RootElement);
        var toolCalls = ReadToolCalls(document.RootElement);

        return new ChatModelResponse(responseId, outputText, toolCalls);
    }

    private static object ToOpenAiInputItem(ChatModelInputMessage message)
    {
        const string toolRolePrefix = "tool:";
        if (message.Role.StartsWith(toolRolePrefix, StringComparison.Ordinal))
        {
            return new
            {
                type = "function_call_output",
                call_id = message.Role[toolRolePrefix.Length..],
                output = message.Content
            };
        }

        return new
        {
            role = message.Role,
            content = message.Content
        };
    }

    private static string? ReadOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) &&
            !string.IsNullOrWhiteSpace(outputText.GetString()))
        {
            return outputText.GetString();
        }

        if (!root.TryGetProperty("output", out var outputCollection)) return null;
        foreach (var output in outputCollection.EnumerateArray())
        {
            if (!output.TryGetProperty("content", out var content)) continue;
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var text) && !string.IsNullOrWhiteSpace(text.GetString()))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<ChatModelToolCall> ReadToolCalls(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var outputCollection)) return [];

        var calls = new List<ChatModelToolCall>();
        foreach (var output in outputCollection.EnumerateArray())
        {
            if (!output.TryGetProperty("type", out var type) ||
                type.GetString() != "function_call")
            {
                continue;
            }

            var name = output.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var callId = output.TryGetProperty("call_id", out var callIdElement) ? callIdElement.GetString() : null;
            var arguments = output.TryGetProperty("arguments", out var argumentsElement) ? argumentsElement.GetString() : null;
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(callId))
            {
                calls.Add(new ChatModelToolCall(callId, name, arguments ?? "{}"));
            }
        }

        return calls;
    }
}
