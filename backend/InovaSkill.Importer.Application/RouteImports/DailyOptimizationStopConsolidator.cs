namespace InovaSkill.Importer.Application.RouteImports;

public static class DailyOptimizationStopConsolidator
{
    public static IReadOnlyList<DailyOptimizationStop> Consolidate(IEnumerable<DailyOptimizationStop> stops) =>
        stops
            .GroupBy(stop => stop.MunicipalityId)
            .Select(group =>
            {
                var first = group
                    .OrderBy(stop => stop.OriginalRouteId)
                    .ThenBy(stop => stop.OriginalSequence)
                    .First();

                return first with
                {
                    LoadKg = group.Sum(stop => stop.LoadKg),
                    Deliveries = group.Sum(stop => stop.Deliveries)
                };
            })
            .ToArray();
}
