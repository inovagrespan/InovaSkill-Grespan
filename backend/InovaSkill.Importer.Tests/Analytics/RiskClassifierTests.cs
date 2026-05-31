using InovaSkill.Importer.Application.Analytics;

namespace InovaSkill.Importer.Tests.Analytics;

public sealed class RiskClassifierTests
{
    [Theory]
    [MemberData(nameof(RiskScores))]
    public void ShouldClassifyRiskScore(decimal? score, string expected)
    {
        Assert.Equal(expected, RiskClassifier.Classify(score));
    }

    public static TheoryData<decimal?, string> RiskScores()
    {
        return new TheoryData<decimal?, string>
        {
            { null, RiskClassifier.InsufficientHistory },
            { 1.5m, RiskClassifier.NoRisk },
            { 2.0m, RiskClassifier.Attention },
            { 3.0m, RiskClassifier.AtRisk },
            { 3.1m, RiskClassifier.Critical }
        };
    }
}
