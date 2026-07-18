using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using LevelPolicy = InovaSkill.Importer.Application.RouteImports.RouteOccupancyLevelPolicy;

namespace InovaSkill.Importer.Infrastructure.Detection;

public sealed class RouteOccupancyDetector(
    ImportDbContext dbContext) : IDetector
{
    public string Code => DetectorCodes.RouteOccupancyAnomaly;

    private const int OccupancyPercentDecimalPlaces = 1;

    public async Task<DetectionResult> DetectAsync(
        DetectionContext context,
        CancellationToken cancellationToken)
    {
        var dataSource = await dbContext.DataSources.AsNoTracking()
            .Where(s => s.Code == RouteImportCodes.DataSource
                && s.CurrentImportId != null)
            .Select(s => new { s.CurrentImportId, s.CurrentImport!.Version })
            .SingleOrDefaultAsync(cancellationToken);

        if (dataSource is null)
        {
            return new DetectionResult(0, Array.Empty<FindingCandidate>());
        }

        var routes = await dbContext.Routes.AsNoTracking()
            .Where(r => r.ImportId == dataSource.CurrentImportId
                && r.OccupancyStatus == RouteOccupancyStatus.Calculated
                && r.OverallOccupancy.HasValue
                && r.VehicleType != null)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Weekday,
                r.OverallOccupancy,
                r.WeightOccupancy,
                r.VolumeOccupancy,
                r.PalletOccupancy,
                r.TotalWeightKg,
                VehicleTypeName = r.VehicleType!.Name,
                r.VehicleType.CapacityKg,
                CityCount = r.Entries.Count
            })
            .ToListAsync(cancellationToken);

        var criticalRoutes = routes
            .Where(r => LevelPolicy.Classify(r.OverallOccupancy) == "critical")
            .OrderByDescending(r => r.OverallOccupancy)
            .ToList();

        var idleRoutes = routes
            .Where(r => LevelPolicy.Classify(r.OverallOccupancy) == "idle")
            .OrderBy(r => r.OverallOccupancy)
            .ToList();

        var findings = new List<FindingCandidate>();
        var now = context.ReferenceTime;

        foreach (var route in criticalRoutes)
        {
            var occupancyPercent = Math.Round(route.OverallOccupancy!.Value * 100, OccupancyPercentDecimalPlaces);
            findings.Add(new FindingCandidate(
                Fingerprint: $"{Code}:CRITICAL:{route.Id}",
                Title: "Rota com ocupação crítica",
                Description: $"A rota '{route.Name}' ({route.Weekday}) está com {occupancyPercent}% de ocupação, excedendo a capacidade do veículo {route.VehicleTypeName} ({route.CapacityKg} kg).",
                SubjectType: "Route",
                SubjectId: route.Id.ToString(),
                SubjectLabel: $"{route.Name} ({route.Weekday})",
                Evidences: new List<FindingEvidenceCandidate>
                {
                    new(
                        Name: "Ocupação geral",
                        Value: occupancyPercent.ToString("F1"),
                        ReferenceValue: (LevelPolicy.CriticalMinimumExclusive * 100).ToString("F0"),
                        Unit: "%",
                        Description: "Percentual de ocupação sobre a capacidade do veículo",
                        SourceType: "Route",
                        SourceId: route.Id.ToString(),
                        ObservedAt: now),
                    new(
                        Name: "Peso total",
                        Value: route.TotalWeightKg.ToString("F0"),
                        ReferenceValue: route.CapacityKg?.ToString("F0") ?? "N/A",
                        Unit: "kg",
                        Description: "Peso total da rota vs capacidade do veículo",
                        SourceType: "Route",
                        SourceId: route.Id.ToString(),
                        ObservedAt: now),
                    new(
                        Name: "Cidades na rota",
                        Value: route.CityCount.ToString(),
                        ReferenceValue: null,
                        Unit: "cidades",
                        Description: "Quantidade de cidades atendidas pela rota",
                        SourceType: "Route",
                        SourceId: route.Id.ToString(),
                        ObservedAt: now),
                    new(
                        Name: "Veículo",
                        Value: route.VehicleTypeName,
                        ReferenceValue: null,
                        Unit: null,
                        Description: "Tipo de veículo utilizado",
                        SourceType: "VehicleType",
                        SourceId: route.VehicleTypeName,
                        ObservedAt: now)
                }));
        }

        foreach (var route in idleRoutes)
        {
            var occupancyPercent = Math.Round(route.OverallOccupancy!.Value * 100, OccupancyPercentDecimalPlaces);
            findings.Add(new FindingCandidate(
                Fingerprint: $"{Code}:IDLE:{route.Id}",
                Title: "Rota com ocupação ociosa",
                Description: $"A rota '{route.Name}' ({route.Weekday}) está com apenas {occupancyPercent}% de ocupação do veículo {route.VehicleTypeName} ({route.CapacityKg} kg).",
                SubjectType: "Route",
                SubjectId: route.Id.ToString(),
                SubjectLabel: $"{route.Name} ({route.Weekday})",
                Evidences: new List<FindingEvidenceCandidate>
                {
                    new(
                        Name: "Ocupação geral",
                        Value: occupancyPercent.ToString("F1"),
                        ReferenceValue: (LevelPolicy.MediumMinimum * 100).ToString("F0"),
                        Unit: "%",
                        Description: "Percentual de ocupação (abaixo de 60% é considerado ocioso)",
                        SourceType: "Route",
                        SourceId: route.Id.ToString(),
                        ObservedAt: now),
                    new(
                        Name: "Peso total",
                        Value: route.TotalWeightKg.ToString("F0"),
                        ReferenceValue: route.CapacityKg?.ToString("F0") ?? "N/A",
                        Unit: "kg",
                        Description: "Peso total da rota vs capacidade do veículo",
                        SourceType: "Route",
                        SourceId: route.Id.ToString(),
                        ObservedAt: now),
                    new(
                        Name: "Ocupação de peso",
                        Value: (route.WeightOccupancy.HasValue
                            ? Math.Round(route.WeightOccupancy.Value * 100, OccupancyPercentDecimalPlaces).ToString("F1")
                            : "N/A"),
                        ReferenceValue: null,
                        Unit: "%",
                        Description: "Ocupação específica por peso",
                        SourceType: "Route",
                        SourceId: route.Id.ToString(),
                        ObservedAt: now),
                    new(
                        Name: "Cidades na rota",
                        Value: route.CityCount.ToString(),
                        ReferenceValue: null,
                        Unit: "cidades",
                        Description: "Quantidade de cidades atendidas pela rota",
                        SourceType: "Route",
                        SourceId: route.Id.ToString(),
                        ObservedAt: now)
                }));
        }

        return new DetectionResult(
            AnalyzedItems: routes.Count,
            Findings: findings.AsReadOnly());
    }
}
