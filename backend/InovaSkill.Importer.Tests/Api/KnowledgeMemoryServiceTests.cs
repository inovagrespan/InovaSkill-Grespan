using InovaSkill.Importer.Api.Assistant;

namespace InovaSkill.Importer.Tests.Api;

public sealed class KnowledgeMemoryServiceTests
{
    [Fact]
    public void CosineSimilarity_RanksEquivalentMeaningVectorAboveOppositeVector()
    {
        var query = new[] { 1f, 0f, 1f };
        var equivalent = KnowledgeMemoryService.CosineSimilarity(query, new[] { 2f, 0f, 2f });
        var opposite = KnowledgeMemoryService.CosineSimilarity(query, new[] { -1f, 0f, -1f });
        Assert.Equal(1d, equivalent, 10);
        Assert.Equal(-1d, opposite, 10);
    }

    [Fact]
    public void CosineSimilarity_ReturnsZeroForInvalidOrZeroVectors()
    {
        Assert.Equal(0, KnowledgeMemoryService.CosineSimilarity([0f, 0f], [1f, 1f]));
        Assert.Equal(0, KnowledgeMemoryService.CosineSimilarity([1f], [1f, 1f]));
    }

    [Theory]
    [InlineData("Qual é o meu nome?", "user", "name", 0.10, true)]
    [InlineData("Onde eu moro?", "user", "location", 0.10, true)]
    [InlineData("O que você lembra sobre mim?", "user", "favorite format", 0.24, true)]
    [InlineData("Quais rotas estão críticas?", "user", "favorite format", 0.24, false)]
    [InlineData("Qual é o meu nome?", "company", "name", 0.24, false)]
    [InlineData("Quais rotas estão críticas?", "company", "route policy", 0.36, true)]
    public void IsRelevantForRecall_UsesSubjectAndPersonalContextWithoutLoweringCompanyThreshold(
        string query,
        string scope,
        string subject,
        double similarity,
        bool expected)
    {
        Assert.Equal(expected, KnowledgeMemoryService.IsRelevantForRecall(query, scope, subject, similarity));
    }

    [Fact]
    public void SelectMemoriesForRecall_IncludesPersonalMemoryWhenQuestionExplicitlyMatchesItsSubject()
    {
        var memories = new[]
        {
            Memory("user", "name", "O nome é Leonardo.", 0.03),
            Memory("user", "role", "Atua como gestor de logística.", 0.01),
            Memory("company", "route policy", "Rotas acima de 95% são críticas.", 0.12),
        };

        var selected = KnowledgeMemoryService.SelectMemoriesForRecall("Qual é o meu nome?", memories);

        Assert.Single(selected);
        Assert.Equal("name", selected[0].Subject);
    }

    [Fact]
    public void SelectMemoriesForRecall_DoesNotIncludeIrrelevantMemoriesToFillTheLimit()
    {
        var memories = new[]
        {
            Memory("user", "name", "O nome é Leonardo.", 0.01),
            Memory("user", "role", "Atua como gestor de logística.", 0.01),
            Memory("company", "invoice policy", "Política fiscal vigente.", 0.90),
        };

        var selected = KnowledgeMemoryService.SelectMemoriesForRecall("Consulte as notas fiscais.", memories);

        Assert.Single(selected);
        Assert.Equal("invoice policy", selected[0].Subject);
    }

    [Fact]
    public void SelectMemoriesForRecall_ReturnsOnlyRelevantMemoriesWhenFewerThanTheLimitQualify()
    {
        var memories = Enumerable.Range(1, 10)
            .Select(index => Memory("company", $"policy {index}", $"Política {index}", index <= 4 ? 0.80 : 0.10))
            .ToArray();

        var selected = KnowledgeMemoryService.SelectMemoriesForRecall("políticas", memories);

        Assert.Equal(4, selected.Count);
        Assert.All(selected, memory => Assert.True(memory.Similarity >= 0.80));
    }

    [Fact]
    public void SelectMemoriesForRecall_ReturnsTheThirtyMostRelevantMemoriesWhenLimitIsExceeded()
    {
        var memories = Enumerable.Range(1, 35)
            .Select(index => Memory("company", $"policy {index}", $"Política {index}", index / 100d + 0.40))
            .ToArray();

        var selected = KnowledgeMemoryService.SelectMemoriesForRecall("políticas", memories);

        Assert.Equal(30, selected.Count);
        Assert.Equal(memories.OrderByDescending(memory => memory.Similarity).Take(30).Select(memory => memory.Id), selected.Select(memory => memory.Id));
        Assert.Equal(0.75, selected.First().Similarity, 10);
        Assert.Equal(0.46, selected.Last().Similarity, 10);
    }

    private static RecalledMemory Memory(string scope, string subject, string content, double similarity) =>
        new(Guid.NewGuid(), scope, subject, content, similarity);
}
