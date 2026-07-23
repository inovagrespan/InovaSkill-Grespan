using System.Text.Json;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Assistant;

public sealed class BusinessAssistantService(
    IChatModelClient modelClient,
    IChatHistoryStore historyStore,
    IEnumerable<IChatTool> tools,
    IOptions<AssistantOptions> options,
    ILogger<BusinessAssistantService> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<string> DefaultSuggestions =
    [
        "Quais rotas estão críticas?",
        "Procure o cliente Marília.",
        "Quais produtos estão em ruptura?",
        "Qual é a taxa de devolução dos últimos 30 dias?"
    ];

    private readonly AssistantOptions assistantOptions = options.Value;
    private readonly IReadOnlyDictionary<string, IChatTool> toolRegistry =
        tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);

    public async Task<AssistantAnswerResponse> AnswerAsync(
        Guid? sessionId,
        string question,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = await historyStore.LoadOrCreateAsync(
            sessionId,
            context.UserId,
            assistantOptions.MaximumHistoryMessages,
            cancellationToken);
        await historyStore.AppendAsync(session.SessionId, "user", question, cancellationToken);

        var messages = session.Messages
            .Append(new ChatModelInputMessage("user", question))
            .ToList();

        var consultedTools = new List<string>();
        var response = await SendToModelAsync(
            new ChatModelRequest(
                assistantOptions.Model,
                AssistantPrompts.LogisticsSystemPrompt,
                messages,
                toolRegistry.Values.Select(ToDefinition).ToList()),
            cancellationToken);

        var toolExecutions = 0;
        while (response.ToolCalls.Count > 0)
        {
            var toolOutputMessages = new List<ChatModelInputMessage>();
            foreach (var call in response.ToolCalls)
            {
                toolExecutions++;
                if (toolExecutions > assistantOptions.MaximumToolExecutionsPerMessage)
                {
                    return await SaveControlledAnswerAsync(
                        session.SessionId,
                        "Não consegui concluir a análise porque a pergunta exigiu consultas demais. Reformule com um escopo menor.",
                        consultedTools,
                        cancellationToken);
                }

                var toolResult = await ExecuteToolAsync(call, context, cancellationToken);
                if (toolRegistry.ContainsKey(call.Name) &&
                    !consultedTools.Contains(call.Name, StringComparer.Ordinal))
                {
                    consultedTools.Add(call.Name);
                }

                toolOutputMessages.Add(new ChatModelInputMessage(
                    $"tool:{call.CallId}",
                    JsonSerializer.Serialize(toolResult.Payload, SerializerOptions)));
            }

            messages = toolOutputMessages;
            response = await SendToModelAsync(
                new ChatModelRequest(
                    assistantOptions.Model,
                    AssistantPrompts.LogisticsSystemPrompt,
                    messages,
                    toolRegistry.Values.Select(ToDefinition).ToList(),
                    response.ResponseId),
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(response.Text))
        {
            return await SaveControlledAnswerAsync(
                session.SessionId,
                "Não consegui gerar uma resposta confiável com os dados disponíveis.",
                consultedTools,
                cancellationToken);
        }

        return await SaveControlledAnswerAsync(
            session.SessionId,
            response.Text.Trim(),
            consultedTools,
            cancellationToken);
    }

    private async Task<AssistantAnswerResponse> SaveControlledAnswerAsync(
        Guid sessionId,
        string answer,
        IReadOnlyList<string> consultedTools,
        CancellationToken cancellationToken)
    {
        await historyStore.AppendAsync(sessionId, "assistant", answer, cancellationToken);
        if (consultedTools.Count > 0)
        {
            logger.LogInformation(
                "Assistente respondeu sessão {SessionId} consultando ferramentas: {Tools}.",
                sessionId,
                string.Join(", ", consultedTools));
        }

        return new AssistantAnswerResponse(
            sessionId,
            answer,
            [],
            DefaultSuggestions,
            "IA com dados reais de rotas");
    }

    private async Task<ChatModelResponse> SendToModelAsync(
        ChatModelRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await modelClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Timeout ao consultar OpenAI no assistente.");
            return new ChatModelResponse(
                string.Empty,
                "A consulta demorou mais que o esperado. Tente novamente com uma pergunta mais específica.",
                []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha controlada ao consultar OpenAI no assistente.");
            return new ChatModelResponse(
                string.Empty,
                "O serviço de IA está indisponível no momento. Tente novamente em instantes.",
                []);
        }
    }

    private async Task<ChatToolResult> ExecuteToolAsync(
        ChatModelToolCall call,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!toolRegistry.TryGetValue(call.Name, out var tool))
        {
            logger.LogWarning("OpenAI solicitou ferramenta inexistente {ToolName}.", call.Name);
            return ChatToolResult.Fail("Ferramenta não disponível.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(assistantOptions.ToolTimeoutSeconds));

        try
        {
            return await tool.ExecuteAsync(call.ArgumentsJson, context, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Timeout ao executar ferramenta {ToolName}.", call.Name);
            return ChatToolResult.Fail("A consulta da ferramenta excedeu o tempo limite.");
        }
    }

    private static ChatToolDefinition ToDefinition(IChatTool tool) =>
        new(tool.Name, tool.Description, tool.GetParameterSchema());

}
