using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RouteLoadPolicyTests
{
    [Theory]
    [InlineData("749.191999999999", "749.192")]
    [InlineData("875.046666666665", "875.047")]
    [InlineData("1.2345", "1.235")]
    [InlineData("-1.2345", "-1.235")]
    [InlineData("0", "0")]
    public void Normalize_UsesThreeDecimalsWithExplicitMidpointRule(
        string input,
        string expected)
    {
        Assert.Equal(decimal.Parse(expected), RouteLoadPolicy.Normalize(decimal.Parse(input)));
    }

    [Fact]
    public void NormalizeThenSum_MatchesPersistedPartsExactly()
    {
        decimal[] source =
        [
            749.191999999999m,
            914.36m,
            875.046666666665m,
            208.453333333333m,
            338.94m
        ];

        var persistedParts = source.Select(RouteLoadPolicy.Normalize).ToArray();
        var total = persistedParts.Sum();

        Assert.Equal(3_085.992m, total);
        Assert.Equal(persistedParts.Sum(), total);
    }
}
