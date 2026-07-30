using InovaSkill.Importer.Api.Assistant;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.Api;

public sealed class BusinessAssistantServiceTests
{
    private static readonly Guid SessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task AnswerAsync_ReturnsDirectModelAnswerWithoutTool()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", "Resposta direta.", [])
        ]);
        var service = CreateService(model, []);

        var response = await service.AnswerAsync(null, "Olá", new ChatExecutionContext(1, "logistica"), default);

        Assert.Equal("Resposta direta.", response.Answer);
        Assert.Equal(SessionId, response.SessionId);
    }

    [Fact]
    public async Task AnswerAsync_ExecutesOneToolAndReturnsFinalAnswer()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", null, [new ChatModelToolCall("call-1", "get_critical_routes", "{\"limit\":2}")]),
            new ChatModelResponse("response-2", "Há uma rota crítica.", [])
        ]);
        var tool = new FakeTool("get_critical_routes", ChatToolResult.Ok(new[] { new { name = "Rota A" } }, 1));
        var service = CreateService(model, [tool]);

        var response = await service.AnswerAsync(null, "Quais rotas estão críticas?", new ChatExecutionContext(1, "logistica"), default);

        Assert.Equal("Há uma rota crítica.", response.Answer);
        Assert.Empty(response.Sources);
        Assert.Equal(1, tool.Executions);
        Assert.Contains(model.Requests[1].Messages, item => item.Role == "tool:call-1");
    }

    [Fact]
    public async Task AnswerAsync_ExecutesMultipleTools()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", null, [
                new ChatModelToolCall("call-1", "search_routes", "{\"searchTerm\":\"Rota\",\"limit\":1}"),
                new ChatModelToolCall("call-2", "get_critical_routes", "{\"limit\":1}")
            ]),
            new ChatModelResponse("response-2", "Resposta consolidada.", [])
        ]);
        var firstTool = new FakeTool("search_routes", ChatToolResult.Ok(Array.Empty<object>(), 0));
        var secondTool = new FakeTool("get_critical_routes", ChatToolResult.Ok(Array.Empty<object>(), 0));
        var service = CreateService(model, [firstTool, secondTool]);

        await service.AnswerAsync(null, "Compare rotas", new ChatExecutionContext(1, "logistica"), default);

        Assert.Equal(1, firstTool.Executions);
        Assert.Equal(1, secondTool.Executions);
    }

    [Fact]
    public async Task AnswerAsync_HandlesUnknownTool()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", null, [new ChatModelToolCall("call-1", "missing_tool", "{}")]),
            new ChatModelResponse("response-2", "Não há ferramenta para isso.", [])
        ]);
        var service = CreateService(model, []);

        var response = await service.AnswerAsync(null, "Use algo inexistente", new ChatExecutionContext(1, "logistica"), default);

        Assert.Equal("Não há ferramenta para isso.", response.Answer);
    }

    [Fact]
    public async Task AnswerAsync_StopsWhenToolLimitIsExceeded()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", null, [
                new ChatModelToolCall("call-1", "search_routes", "{\"searchTerm\":\"Rota\",\"limit\":1}"),
                new ChatModelToolCall("call-2", "search_routes", "{\"searchTerm\":\"Rota\",\"limit\":1}")
            ])
        ]);
        var service = CreateService(
            model,
            [new FakeTool("search_routes", ChatToolResult.Ok(Array.Empty<object>(), 0))],
            new AssistantOptions { MaximumToolExecutionsPerMessage = 1 });

        var response = await service.AnswerAsync(null, "Loop", new ChatExecutionContext(1, "logistica"), default);

        Assert.Contains("consultas demais", response.Answer);
    }

    [Fact]
    public async Task AnswerAsync_ReturnsControlledMessageWhenModelTimesOut()
    {
        var model = new FakeModelClient([], throwOnSend: new OperationCanceledException());
        var service = CreateService(model, []);

        var response = await service.AnswerAsync(null, "Olá", new ChatExecutionContext(1, "logistica"), default);

        Assert.Contains("demorou mais que o esperado", response.Answer);
    }

    [Fact]
    public async Task AnswerAsync_PropagatesRequestCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var service = CreateService(new FakeModelClient([]), []);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.AnswerAsync(null, "Olá", new ChatExecutionContext(1, "logistica"), cancellation.Token));
    }

    [Fact]
    public async Task AnswerAsync_ContinuesAfterToolErrorPayload()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", null, [new ChatModelToolCall("call-1", "search_routes", "{}")]),
            new ChatModelResponse("response-2", "Informe um termo maior.", [])
        ]);
        var service = CreateService(model, [new FakeTool("search_routes", ChatToolResult.Fail("Argumentos inválidos."))]);

        var response = await service.AnswerAsync(null, "R", new ChatExecutionContext(1, "logistica"), default);

        Assert.Equal("Informe um termo maior.", response.Answer);
    }

    [Fact]
    public async Task AnswerAsync_InstructsModelToCalculateOnlySimpleMetricsFromReturnedData()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", "Resposta calculada.", [])
        ]);
        var service = CreateService(model, []);

        await service.AnswerAsync(null, "Calcule uma média simples.", new ChatExecutionContext(1, "logistica"), default);

        Assert.Contains("Você pode fazer cálculos simples na hora somente sobre dados retornados", model.Requests[0].Instructions);
        Assert.Contains("Não faça cálculo na hora se faltar dado", model.Requests[0].Instructions);
        Assert.Contains("Quando uma métrica recorrente ou executiva não estiver disponível", model.Requests[0].Instructions);
    }

    [Fact]
    public async Task AnswerAsync_RequiresEvidencePeriodAndClarificationForAmbiguousQuestions()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", "Qual cliente e período você deseja analisar?", [])
        ]);
        var service = CreateService(model, []);

        await service.AnswerAsync(null, "Analise o resultado.", new ChatExecutionContext(1, "logistica"), default);

        var instructions = model.Requests[0].Instructions;
        Assert.Contains("Nunca complete lacunas", instructions);
        Assert.Contains("Dados insuficientes", instructions);
        Assert.Contains("Dados reais:", instructions);
        Assert.Contains("Interpretação da IA:", instructions);
        Assert.Contains("Período dos dados:", instructions);
        Assert.Contains("Faça uma pergunta curta de esclarecimento", instructions);
    }

    private static BusinessAssistantService CreateService(
        FakeModelClient model,
        IReadOnlyList<IChatTool> tools,
        AssistantOptions? options = null) =>
        new(
            model,
            new FakeHistoryStore(),
            tools,
            Options.Create(options ?? new AssistantOptions()),
            NullLogger<BusinessAssistantService>.Instance);

    private sealed class FakeModelClient(
        IReadOnlyList<ChatModelResponse> responses,
        Exception? throwOnSend = null) : IChatModelClient
    {
        private int index;
        public List<ChatModelRequest> Requests { get; } = [];

        public Task<ChatModelResponse> SendAsync(ChatModelRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (throwOnSend is not null) throw throwOnSend;
            return Task.FromResult(responses[index++]);
        }
    }

    private sealed class FakeHistoryStore : IChatHistoryStore
    {
        public Task<ChatSessionSnapshot> LoadOrCreateAsync(
            Guid? sessionId,
            long userId,
            int maximumMessages,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatSessionSnapshot(sessionId ?? SessionId, []));
        }

        public Task AppendAsync(Guid sessionId, string role, string content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ChatSessionSummary>> ListAsync(
            long userId,
            int maximumSessions,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ChatSessionSummary>>([]);

        public Task<ChatSessionHistory?> LoadAsync(
            Guid sessionId,
            long userId,
            int maximumMessages,
            CancellationToken cancellationToken) =>
            Task.FromResult<ChatSessionHistory?>(null);
    }

    private sealed class FakeTool(string name, ChatToolResult result) : IChatTool
    {
        public int Executions { get; private set; }
        public string Name => name;
        public string Description => name;
        public object GetParameterSchema() => new { type = "object" };

        public Task<ChatToolResult> ExecuteAsync(
            string argumentsJson,
            ChatExecutionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Executions++;
            return Task.FromResult(result);
        }
    }
}
