using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Assistant;

public sealed record RecalledMemory(Guid Id, string Scope, string Subject, string Content, double Similarity);
internal sealed record ExtractedMemory(string Scope, string Subject, string Content);

public sealed class KnowledgeMemoryService(
    ImportDbContext dbContext,
    IChatModelClient modelClient,
    IHttpClientFactory httpClientFactory,
    IOptions<AssistantOptions> options,
    ILogger<KnowledgeMemoryService> logger)
{
    private const int MaximumRecalledMemories = 8;
    private const double MinimumSimilarity = 0.35;
    private const double MinimumPersonalQuerySimilarity = 0.20;
    private static readonly IReadOnlyDictionary<string, string[]> PersonalSubjectTerms =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = ["name", "nome", "chamar", "chamo"],
            ["preferred name"] = ["name", "nome", "chamar", "chamo", "apelido"],
            ["role"] = ["role", "cargo", "funcao", "trabalho", "profissao"],
            ["location"] = ["location", "local", "cidade", "moro", "resido"],
            ["preference"] = ["preference", "preferencia", "prefiro", "gosto"]
        };
    private static readonly HashSet<string> PersonalReferenceTerms =
        new(["eu", "meu", "minha", "meus", "minhas", "me", "mim"], StringComparer.Ordinal);
    private static readonly Regex SecretPattern = new(
        @"(?ix)(password|senha|token|api[_ -]?key|secret|chave\s+privada)\s*[:=]\s*\S+|-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AssistantOptions assistantOptions = options.Value;

    public async Task<string> CreateEmbeddingJsonAsync(string input, CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(await CreateEmbeddingAsync(input, cancellationToken));

    public async Task<IReadOnlyList<RecalledMemory>> RecallAsync(long userId, string query, CancellationToken cancellationToken)
    {
        var queryEmbedding = await CreateEmbeddingAsync(query, cancellationToken);
        var candidates = await dbContext.KnowledgeMemories.AsNoTracking()
            .Where(memory => memory.IsActive &&
                (memory.Scope == KnowledgeMemoryScopes.Company ||
                 (memory.Scope == KnowledgeMemoryScopes.User && memory.OwnerUserId == userId)))
            .OrderByDescending(memory => memory.UpdatedAt)
            .Take(500)
            .Select(memory => new { memory.Id, memory.Scope, memory.Subject, memory.Content, memory.EmbeddingJson })
            .ToListAsync(cancellationToken);

        return candidates.Select(memory => new RecalledMemory(
                memory.Id, memory.Scope, memory.Subject, memory.Content,
                CosineSimilarity(queryEmbedding, DeserializeEmbedding(memory.EmbeddingJson))))
            .Where(memory => IsRelevantForRecall(query, memory.Scope, memory.Subject, memory.Similarity))
            .OrderByDescending(memory => memory.Similarity)
            .Take(MaximumRecalledMemories)
            .ToList();
    }

    public async Task LearnAsync(long userId, Guid sessionId, string question, string answer, string model, CancellationToken cancellationToken)
    {
        if (SecretPattern.IsMatch(question))
        {
            logger.LogInformation("Memorização ignorada por possível segredo na sessão {SessionId}.", sessionId);
            return;
        }

        var response = await modelClient.SendAsync(new ChatModelRequest(
            model,
            """
            Extraia apenas fatos duráveis explicitamente informados pelo usuário e úteis em conversas futuras.
            Classifique como company quando o fato for sobre a Grespan e como user quando for preferência, função ou contexto pessoal do autor.
            Ignore perguntas, hipóteses, inferências, dados operacionais transitórios, conteúdo da resposta do assistente e qualquer senha, token, chave ou segredo.
            Use um subject curto e estável para que uma informação posterior sobre o mesmo assunto substitua a anterior.
            Retorne somente o JSON solicitado.
            """,
            [new ChatModelInputMessage("user", $"Mensagem do usuário:\n{question}\n\nResposta apenas para contexto (não extrair fatos dela):\n{answer}")],
            [],
            Purpose: ChatModelRequestPurpose.MemoryExtraction,
            TextFormat: ExtractionSchema()), cancellationToken);

        foreach (var extracted in ParseExtraction(response.Text))
        {
            if (SecretPattern.IsMatch(extracted.Content)) continue;
            await UpsertAsync(userId, sessionId, extracted, cancellationToken);
        }
    }

    private async Task UpsertAsync(long userId, Guid sessionId, ExtractedMemory extracted, CancellationToken cancellationToken)
    {
        var scope = extracted.Scope == KnowledgeMemoryScopes.User ? KnowledgeMemoryScopes.User : KnowledgeMemoryScopes.Company;
        long? ownerUserId = scope == KnowledgeMemoryScopes.User ? userId : null;
        var existing = await dbContext.KnowledgeMemories.FirstOrDefaultAsync(memory =>
            memory.IsActive && memory.Scope == scope && memory.OwnerUserId == ownerUserId && memory.Subject == extracted.Subject,
            cancellationToken);
        if (existing is not null && string.Equals(existing.Content, extracted.Content, StringComparison.OrdinalIgnoreCase)) return;

        var sourceMessageId = await dbContext.ChatMessages.AsNoTracking()
            .Where(message => message.ChatSessionId == sessionId && message.Role == "user")
            .OrderByDescending(message => message.CreatedAt)
            .Select(message => message.Id)
            .FirstAsync(cancellationToken);
        var now = DateTime.UtcNow;
        if (existing is not null) existing.IsActive = false;
        dbContext.KnowledgeMemories.Add(new KnowledgeMemory
        {
            Id = Guid.NewGuid(), Scope = scope, OwnerUserId = ownerUserId, CreatedByUserId = userId,
            SourceChatMessageId = sourceMessageId, SupersedesMemoryId = existing?.Id,
            Subject = extracted.Subject.Trim(), Content = extracted.Content.Trim(),
            EmbeddingJson = JsonSerializer.Serialize(await CreateEmbeddingAsync($"{extracted.Subject}: {extracted.Content}", cancellationToken)),
            IsActive = true, CreatedAt = now, UpdatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<float[]> CreateEmbeddingAsync(string input, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assistantOptions.OpenAiApiKey);
        request.Content = JsonContent.Create(new { model = assistantOptions.EmbeddingModel, input }, options: JsonOptions);
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return document.RootElement.GetProperty("data")[0].GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }

    public static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length == 0 || left.Length != right.Length) return 0;
        double dot = 0, leftMagnitude = 0, rightMagnitude = 0;
        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index]; leftMagnitude += left[index] * left[index]; rightMagnitude += right[index] * right[index];
        }
        return leftMagnitude == 0 || rightMagnitude == 0 ? 0 : dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    public static bool IsRelevantForRecall(string query, string scope, string subject, double similarity)
    {
        if (similarity >= MinimumSimilarity) return true;
        if (scope != KnowledgeMemoryScopes.User) return false;

        var queryTerms = Tokenize(query);
        if (PersonalSubjectTerms.TryGetValue(NormalizeText(subject), out var subjectTerms) &&
            subjectTerms.Any(queryTerms.Contains))
        {
            return true;
        }

        return similarity >= MinimumPersonalQuerySimilarity && queryTerms.Overlaps(PersonalReferenceTerms);
    }

    private static HashSet<string> Tokenize(string value) =>
        NormalizeText(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    private static string NormalizeText(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return Regex.Replace(withoutDiacritics.Normalize(NormalizationForm.FormC), @"[^a-z0-9]+", " ").Trim();
    }

    private static float[] DeserializeEmbedding(string json) => JsonSerializer.Deserialize<float[]>(json, JsonOptions) ?? [];
    private static IReadOnlyList<ExtractedMemory> ParseExtraction(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("memories").EnumerateArray()
                .Select(item => new ExtractedMemory(item.GetProperty("scope").GetString() ?? "", item.GetProperty("subject").GetString() ?? "", item.GetProperty("content").GetString() ?? ""))
                .Where(item => item.Subject.Length is > 0 and <= 256 && item.Content.Length is > 0 and <= 2000 && item.Scope is KnowledgeMemoryScopes.Company or KnowledgeMemoryScopes.User)
                .Take(5).ToList();
        }
        catch (JsonException) { return []; }
    }

    private static object ExtractionSchema() => new
    {
        type = "json_schema", name = "memory_extraction", strict = true,
        schema = new { type = "object", properties = new { memories = new { type = "array", maxItems = 5, items = new { type = "object", properties = new { scope = new { type = "string", @enum = new[] { "company", "user" } }, subject = new { type = "string", maxLength = 256 }, content = new { type = "string", maxLength = 2000 } }, required = new[] { "scope", "subject", "content" }, additionalProperties = false } } }, required = new[] { "memories" }, additionalProperties = false }
    };
}
