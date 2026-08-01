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
    public void SelectMemoriesForRecall_AlwaysIncludesUpToThreeUserReferenceMemories()
    {
        var memories = new[]
        {
            Memory("user", "preferred name", "Prefere ser chamado de Leo.", 0.02),
            Memory("user", "name", "O nome é Leonardo.", 0.03),
            Memory("user", "role", "Atua como gestor de logística.", 0.01),
            Memory("company", "route policy", "Rotas acima de 95% são críticas.", 0.92),
        };

        var selected = KnowledgeMemoryService.SelectMemoriesForRecall("Quais notas fiscais são recentes?", memories);

        Assert.Equal(4, selected.Count);
        Assert.Equal(["preferred name", "name", "role"], selected.Take(3).Select(memory => memory.Subject));
        Assert.Contains(selected, memory => memory.Subject == "route policy");
    }

    [Fact]
    public void SelectMemoriesForRecall_DoesNotReserveCompanyOrUnrelatedUserMemoriesAsIdentity()
    {
        var memories = new[]
        {
            Memory("company", "name", "Nome de uma política interna.", 0.01),
            Memory("user", "favorite format", "Prefere tabelas.", 0.01),
            Memory("company", "invoice policy", "Política fiscal vigente.", 0.90),
        };

        var selected = KnowledgeMemoryService.SelectMemoriesForRecall("Consulte as notas fiscais.", memories);

        Assert.Single(selected);
        Assert.Equal("invoice policy", selected[0].Subject);
    }

    [Fact]
    public void SelectMemoriesForRecall_KeepsTheGlobalRecallLimit()
    {
        var memories = new[]
        {
            Memory("user", "preferred name", "Leo", 0.01),
            Memory("user", "name", "Leonardo", 0.01),
            Memory("user", "role", "Gestor", 0.01),
        }.Concat(Enumerable.Range(1, 10).Select(index =>
            Memory("company", $"policy {index}", $"Política {index}", 0.90 - index / 100d))).ToArray();

        var selected = KnowledgeMemoryService.SelectMemoriesForRecall("políticas", memories);

        Assert.Equal(8, selected.Count);
        Assert.Equal(3, selected.Count(memory => memory.Scope == "user"));
    }

    private static RecalledMemory Memory(string scope, string subject, string content, double similarity) =>
        new(Guid.NewGuid(), scope, subject, content, similarity);
}
