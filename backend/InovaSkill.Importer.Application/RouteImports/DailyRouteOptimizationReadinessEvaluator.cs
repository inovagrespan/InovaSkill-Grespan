using System.Globalization;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Application.RouteImports;

public static class DailyRouteOptimizationIssueCodes
{
    public const string MunicipalityNotLinked = "MUNICIPALITY_NOT_LINKED";
    public const string InvalidStopWeight = "INVALID_STOP_WEIGHT";
    public const string MunicipalityCoordinateMissing = "MUNICIPALITY_COORDINATE_MISSING";
    public const string VehicleCapacityMissing = "VEHICLE_CAPACITY_MISSING";
}

public sealed record DailyRouteOptimizationReadinessIssue(
    string Code,
    Guid? RouteId,
    Guid? RouteEntryId,
    Guid? MunicipalityId,
    Guid? VehicleTypeId,
    string Message,
    string? CurrentValue,
    bool CanResolve);

public static class DailyRouteOptimizationReadinessEvaluator
{
    public static IReadOnlyList<DailyRouteOptimizationReadinessIssue> Evaluate(IReadOnlyCollection<Route> routes)
    {
        var issues = new List<DailyRouteOptimizationReadinessIssue>();
        foreach (var route in routes.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var capacity = route.VehicleCapacityKgSnapshot;
            if (capacity <= 0)
                issues.Add(new(
                    DailyRouteOptimizationIssueCodes.VehicleCapacityMissing,
                    route.Id,
                    null,
                    null,
                    route.VehicleTypeId,
                    $"O tipo de veículo da rota {route.Name} não possui capacidade válida.",
                    capacity.ToString("0.###", CultureInfo.InvariantCulture),
                    true));

            foreach (var entry in route.Entries
                         .Where(item => !item.IsExcludedFromOptimization || item.AveragePerDay != 0)
                         .OrderBy(item => item.Sequence))
            {
                if (entry.MunicipalityId is null)
                    issues.Add(new(
                        DailyRouteOptimizationIssueCodes.MunicipalityNotLinked,
                        route.Id,
                        entry.Id,
                        null,
                        null,
                        $"A parada {entry.Name} não está vinculada a um município oficial.",
                        entry.Name,
                        true));
                else if (entry.Municipality?.Coordinate is null ||
                         entry.Municipality.Coordinate.Status != MunicipalityCoordinateStatuses.Resolved ||
                         entry.Municipality.Coordinate.Latitude is null ||
                         entry.Municipality.Coordinate.Longitude is null)
                    issues.Add(new(
                        DailyRouteOptimizationIssueCodes.MunicipalityCoordinateMissing,
                        route.Id,
                        entry.Id,
                        entry.MunicipalityId,
                        null,
                        $"O município da parada {entry.Name} não possui coordenada resolvida.",
                        entry.Municipality?.Name ?? entry.Name,
                        true));

                if (entry.AveragePerDay <= 0)
                    issues.Add(new(
                        DailyRouteOptimizationIssueCodes.InvalidStopWeight,
                        route.Id,
                        entry.Id,
                        entry.MunicipalityId,
                        null,
                        entry.AveragePerDay < 0
                            ? $"A parada {entry.Name} possui peso negativo e exige um peso positivo."
                            : $"A parada {entry.Name} possui peso zero; informe um peso positivo ou exclua-a da simulação.",
                        entry.AveragePerDay.ToString("0.###", CultureInfo.InvariantCulture),
                        true));
            }
        }

        return issues;
    }

    public static string Summarize(IReadOnlyCollection<DailyRouteOptimizationReadinessIssue> issues)
    {
        var groups = issues.GroupBy(item => item.Code).ToDictionary(item => item.Key, item => item.Count());
        var parts = new List<string>();
        Add(DailyRouteOptimizationIssueCodes.MunicipalityNotLinked, "município sem vínculo", "municípios sem vínculo");
        Add(DailyRouteOptimizationIssueCodes.InvalidStopWeight, "peso inválido", "pesos inválidos");
        Add(DailyRouteOptimizationIssueCodes.MunicipalityCoordinateMissing, "coordenada ausente", "coordenadas ausentes");
        Add(DailyRouteOptimizationIssueCodes.VehicleCapacityMissing, "capacidade ausente", "capacidades ausentes");
        return parts.Count == 0 ? "Dados insuficientes para otimizar." : string.Join(", ", parts) + ".";

        void Add(string code, string singular, string plural)
        {
            if (!groups.TryGetValue(code, out var count)) return;
            parts.Add($"{count} {(count == 1 ? singular : plural)}");
        }
    }
}
