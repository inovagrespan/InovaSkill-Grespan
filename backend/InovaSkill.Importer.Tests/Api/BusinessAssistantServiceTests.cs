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
    public async Task AnswerAsync_AllowsPersonalIntroductionWithoutScopeClassification()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", "Prazer, Leonardo! Como posso ajudar?", [])
        ], scopeDecision: "OUT_OF_SCOPE");
        var service = CreateService(model, []);

        var response = await service.AnswerAsync(null, "Meu nome é Leonardo", new ChatExecutionContext(1, "logistica"), default);

        Assert.Equal("Prazer, Leonardo! Como posso ajudar?", response.Answer);
        Assert.Empty(model.ScopeRequests);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task AnswerAsync_AllowsAmbiguousMessageSoAssistantCanAskForContext()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", "Claro. Quer me contar um pouco mais?", [])
        ], scopeDecision: "AMBIGUOUS");
        var service = CreateService(model, []);

        var response = await service.AnswerAsync(null, "Aconteceu uma coisa hoje", new ChatExecutionContext(1, "logistica"), default);

        Assert.Equal("Claro. Quer me contar um pouco mais?", response.Answer);
        Assert.Single(model.ScopeRequests);
        Assert.Single(model.Requests);
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
    public async Task AnswerAsync_ReturnsControlledTimeoutWhenScopeClassificationFails()
    {
        var model = new FakeModelClient([], throwOnSend: new OperationCanceledException());
        var service = CreateService(model, []);

        var response = await service.AnswerAsync(null, "Olá", new ChatExecutionContext(1, "logistica"), default);

        Assert.Contains("demorou mais que o esperado", response.Answer);
        Assert.Empty(model.Requests);
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
    public async Task AnswerAsync_IncludesGrespanBusinessContextInModelInstructions()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", "Resposta contextualizada.", [])
        ]);
        var service = CreateService(model, []);

        await service.AnswerAsync(null, "Qual é o contexto da empresa?", new ChatExecutionContext(1, "logistica"), default);

        var instructions = model.Requests[0].Instructions;
        Assert.Contains("Grespan", instructions);
        Assert.Contains("fabricação de pães congelados", instructions);
        Assert.Contains("Considere esse contexto empresarial", instructions);
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
        Assert.Contains("apresente o resultado diretamente", instructions);
        Assert.Contains("sem rótulos ou ressalvas sobre a origem dos dados", instructions);
        Assert.DoesNotContain("Dados reais:", instructions);
        Assert.Contains("priorize o nome preferido", instructions);
        Assert.Contains("cargo ou função", instructions);
        Assert.Contains("Período dos dados:", instructions);
        Assert.Contains("Faça uma pergunta curta de esclarecimento", instructions);
        Assert.Contains("ausência de um campo neles não prova que o dado não existe", instructions);
        Assert.Contains("Faça a consulta primeiro", instructions);
        Assert.Contains("consultar várias ferramentas na mesma resposta", instructions);
        Assert.Contains("uma quantidade considerável de consultas", instructions);
        Assert.Contains("Pare assim que houver evidência suficiente", instructions);
        Assert.Contains("não repita consultas equivalentes", instructions);
        Assert.Contains("consulte novamente list_recent_fiscal_documents", instructions);
        Assert.Contains("[TABELA]", instructions);
        Assert.Contains("[COLUNAS]", instructions);
        Assert.Contains("[LINHA]", instructions);
        Assert.Contains("mesma quantidade e ordem de células", instructions);
    }

    [Fact]
    public async Task AnswerAsync_BlocksQuestionsExplicitlyOutsideGrespanScope()
    {
        var model = new FakeModelClient([], scopeDecision: "OUT_OF_SCOPE");
        var service = CreateService(model, []);

        var response = await service.AnswerAsync(null, "Quem ganhou a eleição?", new ChatExecutionContext(1, "logistica"), default);

        Assert.Contains("fora do contexto da Grespan", response.Answer);
        Assert.Empty(model.Requests);
        Assert.Single(model.ScopeRequests);
        Assert.NotNull(model.ScopeRequests[0].TextFormat);
        Assert.Contains("política", model.ScopeRequests[0].Instructions);
        Assert.Contains(model.ScopeRequests[0].Messages, message => message.Content == "Quem ganhou a eleição?");
    }

    [Fact]
    public async Task AnswerAsync_AllowsInvalidScopeResultSoAssistantCanRecover()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", "Pode me dar um pouco mais de contexto?", [])
        ], scopeDecision: "INVALID");
        var service = CreateService(model, []);

        var response = await service.AnswerAsync(null, "Quero conversar sobre uma situação", new ChatExecutionContext(1, "logistica"), default);

        Assert.Equal("Pode me dar um pouco mais de contexto?", response.Answer);
        Assert.Single(model.ScopeRequests);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task AnswerAsync_UsesIsolatedWebSearchAndReturnsDeduplicatedSources()
    {
        var model = new FakeModelClient([
            new ChatModelResponse("response-1", null, [
                new ChatModelToolCall("research-1", "request_external_research", "{\"publicQuery\":\"normas atuais para armazenamento de pães congelados\"}")
            ]),
            new ChatModelResponse("web-1", "Resumo público.", [], [
                new ChatModelSource("Fonte oficial", "https://example.org/norma"),
                new ChatModelSource("Duplicada", "https://example.org/norma")
            ]),
            new ChatModelResponse("response-2", "Informações externas:\nResumo aplicado à Grespan.\nInterpretação da IA:\nValidar internamente.", [])
        ]);
        var service = CreateService(model, []);

        var response = await service.AnswerAsync(null, "Quais normas atuais podem ajudar o armazenamento da Grespan?", new ChatExecutionContext(1, "logistica"), default);

        Assert.Single(response.Sources);
        Assert.Equal("https://example.org/norma", response.Sources[0].Value);
        var webRequest = Assert.Single(model.Requests, request => request.Purpose == ChatModelRequestPurpose.ExternalResearch);
        Assert.True(webRequest.EnableWebSearch);
        Assert.Single(webRequest.Messages);
        Assert.DoesNotContain("Grespan", webRequest.Messages[0].Content, StringComparison.OrdinalIgnoreCase);
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
            NullLogger<BusinessAssistantService>.Instance,
            new AssistantScopeClassifier(model, NullLogger<AssistantScopeClassifier>.Instance));

    private sealed class FakeModelClient(
        IReadOnlyList<ChatModelResponse> responses,
        Exception? throwOnSend = null,
        string scopeDecision = "IN_SCOPE") : IChatModelClient
    {
        private int index;
        public List<ChatModelRequest> Requests { get; } = [];
        public List<ChatModelRequest> ScopeRequests { get; } = [];

        public Task<ChatModelResponse> SendAsync(ChatModelRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (throwOnSend is not null) throw throwOnSend;
            if (request.Purpose == ChatModelRequestPurpose.ScopeClassification)
            {
                ScopeRequests.Add(request);
                var text = scopeDecision == "INVALID" ? "not-json" : $"{{\"decision\":\"{scopeDecision}\"}}";
                return Task.FromResult(new ChatModelResponse("scope-1", text, []));
            }
            Requests.Add(request);
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
            int offset,
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
