namespace InovaSkill.Importer.Application.RouteImports;

public static class RouteOccupancyLevelPolicy
{
    public const decimal MediumMinimum = 0.60m;
    public const decimal GoodMinimum = 0.85m;
    public const decimal CriticalMinimumExclusive = 0.95m;

    public static string Classify(decimal? overallOccupancy) => overallOccupancy switch
    {
        null => "unavailable",
        > CriticalMinimumExclusive => "critical",
        >= GoodMinimum => "good",
        >= MediumMinimum => "medium",
        _ => "idle"
    };

    public static string Label(string level) => level switch
    {
        "critical" => "Crítico",
        "good" => "Saudável",
        "medium" => "Médio",
        "idle" => "Ocioso",
        "unavailable" => "Indisponível",
        _ => level
    };
}
