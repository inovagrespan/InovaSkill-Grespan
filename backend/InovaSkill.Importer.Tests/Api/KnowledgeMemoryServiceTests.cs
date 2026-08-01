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
}
