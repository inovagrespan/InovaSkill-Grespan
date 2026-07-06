namespace InovaSkill.Importer.Application.RouteImports;

public static class RouteLoadPolicy
{
    public const int DecimalPlaces = 3;

    public static decimal Normalize(decimal loadKg) =>
        decimal.Round(loadKg, DecimalPlaces, MidpointRounding.AwayFromZero);
}
