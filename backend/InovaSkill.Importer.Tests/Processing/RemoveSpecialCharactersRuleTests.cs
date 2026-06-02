using InovaSkill.Importer.Infrastructure.Processing.TransformRules;

namespace InovaSkill.Importer.Tests.Processing;

public class RemoveSpecialCharactersRuleTests
{
    [Fact]
    public void Apply_ShouldRemoveAccentsAndUnsafeSymbols()
    {
        var rule = new RemoveSpecialCharactersRule();

        var result = rule.Apply(" João & Cia! ", null);

        Assert.Equal("Joao  Cia", result);
    }
}
