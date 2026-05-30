using InovaSkill.Importer.Infrastructure.Processing.TransformRules;

namespace InovaSkill.Importer.Tests.Processing;

public class OnlyDigitsRuleTests
{
    [Fact]
    public void Apply_ShouldKeepOnlyDigits()
    {
        var rule = new OnlyDigitsRule();

        var result = rule.Apply("12.345.678/0001-90", null);

        Assert.Equal("12345678000190", result);
    }
}
