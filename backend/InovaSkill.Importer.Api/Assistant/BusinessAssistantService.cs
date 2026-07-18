using System.Net.Http.Headers;
using System.Text.Json;
using InovaSkill.Importer.Domain.Entities;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Assistant;

public sealed class BusinessAssistantService(
    IHttpClientFactory httpClientFactory,
    IOptions<AssistantOptions> options)
{
    private static readonly string[] RouteTerms = ["rota", "rotas", "ocupação", "ocupacao", "sobrecarregada", "ociosa"];
    private static readonly string[] InventoryTerms = ["estoque", "ruptura", "produto", "produtos"];
    private static readonly string[] ImportTerms = ["importação", "importacao", "importações", "importacoes", "planilha", "erro"];
    private static readonly string[] CustomerTerms = ["cliente", "clientes"];
    private readonly AssistantOptions assistantOptions = options.Value;

    public async Task<AssistantAnswerResponse> AnswerAsync(
        string question,
        string role,
        CancellationToken cancellationToken)
    {
        var context = BuildDemonstrationContext(Normalize(question), role);
        var generatedAnswer = await TryGenerateWithOpenAiAsync(question, role, context, cancellationToken);

        return new AssistantAnswerResponse(
            generatedAnswer ?? context.FallbackAnswer,
            context.Sources,
            context.Suggestions,
            generatedAnswer is null ? "Dados demonstrativos" : "IA com dados demonstrativos");
    }

    private static AssistantContext BuildDemonstrationContext(string question, string role)
    {
        if (ContainsAny(question, RouteTerms))
        {
            return new AssistantContext(
                "Na base demonstrativa existem 24 rotas: 3 estão críticas por sobrecarga e 5 estão ociosas. As maiores ocupações são Rota Campinas Norte (118,4%), Rota Sorocaba Centro (109,7%) e Rota Bauru Sul (104,2%).",
                [
                    new("Base analisada", "24 rotas fictícias"),
                    new("Rotas críticas", "3"),
                    new("Rotas ociosas", "5")
                ],
                ["Quais rotas estão ociosas?", "Qual rota tem a maior ocupação?", "Quais produtos estão em ruptura?"],
                JsonSerializer.Serialize(new
                {
                    dataType = "demonstration",
                    totalRoutes = 24,
                    criticalRoutes = 3,
                    idleRoutes = 5,
                    topOccupancy = new[]
                    {
                        new { name = "Rota Campinas Norte", occupancyPercent = 118.4m },
                        new { name = "Rota Sorocaba Centro", occupancyPercent = 109.7m },
                        new { name = "Rota Bauru Sul", occupancyPercent = 104.2m }
                    }
                }));
        }

        if (ContainsAny(question, InventoryTerms))
        {
            return new AssistantContext(
                "Na base demonstrativa existem 186 produtos com posição de estoque e 12 estão em ruptura. Entre os exemplos estão Pão Francês Congelado, Croissant Tradicional, Massa de Sonho e Bolo de Chocolate.",
                [
                    new("Produtos no estoque", "186 fictícios"),
                    new("Produtos em ruptura", "12")
                ],
                ["Quais são os produtos em ruptura?", "Quantos produtos têm estoque disponível?", "Quais rotas estão críticas?"],
                JsonSerializer.Serialize(new
                {
                    dataType = "demonstration",
                    totalProducts = 186,
                    stockouts = 12,
                    examples = new[] { "Pão Francês Congelado", "Croissant Tradicional", "Massa de Sonho", "Bolo de Chocolate" }
                }));
        }

        if (ContainsAny(question, ImportTerms) &&
            role is AppUserRoles.Admin or AppUserRoles.AdminSystem)
        {
            return new AssistantContext(
                "Na base demonstrativa, 2 das 10 importações mais recentes possuem inconsistências. A mais recente é “Estoque_Demonstracao_Julho.xlsx”, concluída com 242 linhas importadas e 8 avisos.",
                [
                    new("Importações analisadas", "10 fictícias"),
                    new("Com inconsistências", "2")
                ],
                ["Qual foi a última importação?", "Quais importações têm erros?", "Quantos produtos estão em ruptura?"],
                JsonSerializer.Serialize(new
                {
                    dataType = "demonstration",
                    analyzedImports = 10,
                    importsWithWarnings = 2,
                    latest = new { fileName = "Estoque_Demonstracao_Julho.xlsx", importedRows = 242, warnings = 8 }
                }));
        }

        if (ContainsAny(question, CustomerTerms))
        {
            return new AssistantContext(
                "A base demonstrativa possui 320 clientes ativos. Desses, 46 compraram nos últimos 30 dias e 18 apresentam redução relevante no volume de compras.",
                [
                    new("Clientes ativos", "320 fictícios"),
                    new("Com compra recente", "46"),
                    new("Em queda de consumo", "18")
                ],
                ["Quantos clientes existem?", "Quais produtos estão em ruptura?", "Quais rotas estão sobrecarregadas?"],
                JsonSerializer.Serialize(new
                {
                    dataType = "demonstration",
                    activeCustomers = 320,
                    recentCustomers = 46,
                    decliningCustomers = 18
                }));
        }

        return new AssistantContext(
            "Estou usando uma base fictícia nesta fase. Posso demonstrar análises de rotas, ocupação, estoque, rupturas, clientes e, para administradores, importações.",
            [new("Ambiente", "Base demonstrativa")],
            SuggestionsForRole(role),
            JsonSerializer.Serialize(new { dataType = "demonstration", matchedDomain = false }));
    }

    private async Task<string?> TryGenerateWithOpenAiAsync(
        string question,
        string role,
        AssistantContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assistantOptions.OpenAiApiKey))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assistantOptions.OpenAiApiKey);
        request.Content = JsonContent.Create(new
        {
            model = assistantOptions.Model,
            instructions = "Você é o assistente demonstrativo do Conecta360. Responda em PT-BR, com objetividade. Todos os números fornecidos são fictícios e você deve deixar isso explícito. Use somente o contexto fornecido. Nunca invente dados adicionais e nunca afirme que executou alterações.",
            input = $"Perfil do usuário: {role}\nPergunta: {question}\nContexto fictício autorizado: {context.ModelContext}"
        });

        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (document.RootElement.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString();
        }

        if (!document.RootElement.TryGetProperty("output", out var outputCollection)) return null;
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

    private static IReadOnlyList<string> SuggestionsForRole(string role) =>
        role is AppUserRoles.Admin or AppUserRoles.AdminSystem
            ? ["Quais importações têm erros?", "Quais rotas estão críticas?", "Quantos produtos estão em ruptura?"]
            : ["Quais produtos estão em ruptura?", "Quantos clientes existem?", "Quais rotas estão críticas?"];

    private static bool ContainsAny(string value, IEnumerable<string> terms) => terms.Any(value.Contains);
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private sealed record AssistantContext(
        string FallbackAnswer,
        IReadOnlyList<AssistantSource> Sources,
        IReadOnlyList<string> Suggestions,
        string ModelContext);
}
