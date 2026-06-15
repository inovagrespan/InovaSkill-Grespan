using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Application.Abstractions;

public interface IRoutePlanningWorkbookParser
{
    RoutePlanningWorkbookParseResult Parse(string filePath);
}

public sealed record RoutePlanningWorkbookParseResult(
    IReadOnlyList<TruckCapacityProfile> TruckCapacities,
    IReadOnlyList<RoutePlan> Routes);
