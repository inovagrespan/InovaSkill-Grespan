using InovaSkill.Importer.Infrastructure.Processing.TransformRules;

namespace InovaSkill.Importer.Tests.Processing;

public class BrazilianDateRuleTests
{
    [Fact]
    public void Apply_ShouldParseDateUsingConfiguredFormats()
    {
        var rule = new BrazilianDateRule();

        var result = rule.Apply("2026-05-29", "{\"formats\":[\"dd/MM/yyyy\",\"yyyy-MM-dd\"]}");

        var parsed = Assert.IsType<DateTime>(result);
        Assert.Equal(new DateTime(2026, 5, 29), parsed);
    }
}
