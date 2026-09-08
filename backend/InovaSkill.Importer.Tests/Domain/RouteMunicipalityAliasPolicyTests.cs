using InovaSkill.Importer.Domain;

namespace InovaSkill.Importer.Tests.Domain;

public sealed class RouteMunicipalityAliasPolicyTests
{
    [Theory]
    [InlineData("FLORESTA DO SUL", "PRESIDENTE PRUDENTE")]
    [InlineData("IGARACU", "IGARACU DO TIETE")]
    [InlineData("ROSALIA", "MARILIA")]
    [InlineData("INDAITUBA", "INDAIATUBA")]
    [InlineData("MARILIA", "MARILIA")]
    public void Resolve_MapsKnownRouteAliasesAndPreservesMunicipalities(string input, string expected) =>
        Assert.Equal(expected, RouteMunicipalityAliasPolicy.Resolve(input));
}
