namespace InovaSkill.Importer.Domain;

public static class RouteMunicipalityAliasPolicy
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["FLORESTA DO SUL"] = "PRESIDENTE PRUDENTE",
        ["IGARACU"] = "IGARACU DO TIETE",
        ["ROSALIA"] = "MARILIA",
        ["(COLINAS) (SAO JOSE DOS CAMPOS)"] = "SAO JOSE DOS CAMPOS",
        ["(OZANAN) (JUNDIAI)"] = "JUNDIAI",
        ["INDAITUBA"] = "INDAIATUBA",
        ["ROMAEL - EMPORIO STA TEREZINHA (MARILIA)"] = "MARILIA"
    };

    public static string Resolve(string normalizedRouteEntryName) =>
        Aliases.GetValueOrDefault(normalizedRouteEntryName, normalizedRouteEntryName);
}
