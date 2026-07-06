using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class MunicipalityNameNormalizerTests
{
    [Theory]
    [InlineData("S\u00C3O JOS\u00C9 DO RIO PRETO", "SAO JOSE DO RIO PRETO")]
    [InlineData("  São   José do Rio Preto ", "SAO JOSE DO RIO PRETO")]
    public void Normalize_CreatesStableMunicipalityKey(string input, string expected) =>
        Assert.Equal(expected, MunicipalityNameNormalizer.Normalize(input));
}
