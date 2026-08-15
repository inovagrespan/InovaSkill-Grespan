using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class SingleRouteOptimizationSolver(IDistanceMatrixProvider distanceMatrixProvider)
    : IRouteOptimizationSolver
{
    private const int OccupancyPercentScale = 100;
    private const int OccupancyPercentDecimalPlaces = 1;
    private const decimal DistanceScoreWeight = 0.05m;

    public bool CanHandle(RouteOptimizationScope scope) => scope == RouteOptimizationScope.SingleRoute;

    public async Task<RouteOptimizationSolution> SolveAsync(
        RouteOptimizationProblem problem,
        CancellationToken cancellationToken)
    {
        if (problem.Scope != RouteOptimizationScope.SingleRoute || problem.TargetRouteId is null)
        {
            throw new InvalidOperationException("O solver de rota única exige TargetRouteId.");
        }

        var source = problem.Routes.SingleOrDefault(route => route.RouteId == problem.TargetRouteId.Value);
        if (source is null)
        {
            return Insufficient("RouteNotFound", "Rota selecionada não encontrada no snapshot informado.");
        }

        var currentMetrics = ToMetrics(source);
        if (source.CapacityKg is not > 0 || source.Occupancy is null)
        {
            return Insufficient(
                "MissingCapacity",
                "Não foi possível gerar uma recomendação confiável porque a rota selecionada não possui capacidade de caminhão configurada.");
        }

        if (source.Cities.Count == 0)
        {
            return Insufficient(
                "MissingRouteCities",
                "Não foi possível gerar uma recomendação confiável porque a rota selecionada não possui cidades.");
        }

        if (source.Cities.Any(city => city.LoadKg < 0))
        {
            return Insufficient(
                "MissingCityDemand",
                "Não foi possível gerar uma recomendação confiável porque não há dados suficientes para calcular o impacto de cada cidade na ocupação da rota.");
        }

        var citiesWithoutCoordinates = problem.Routes
            .SelectMany(route => route.Cities)
            .Count(city => city.Location is null);
        if (source.Cities.All(city => city.Location is null))
        {
            return Insufficient(
                "MissingCoordinates",
                "Não foi possível gerar uma recomendação confiável porque há cidades sem latitude e longitude.");
        }

        if (source.Occupancy <= RouteOccupancyLevelPolicy.CriticalMinimumExclusive)
        {
            var scenario = new RouteOptimizationScenarioCandidate(
                Score: 0,
                ActionType: RouteOptimizationActionType.NoChange,
                Confidence: RouteOptimizationConfidence.Medium,
                EstimatedDistanceChangeKm: null,
                CurrentMetrics: currentMetrics,
                ProposedMetrics: currentMetrics,
                Reasons:
                [
                    new RouteOptimizationReasonDto(
                        "NoMeaningfulImprovement",
                        $"A rota já está em {FormatPercent(source.Occupancy)} de ocupação e não precisa de alteração.")
                ],
                Warnings: ["Simulação: nenhuma alteração foi aplicada às rotas atuais."],
                CityReallocations: [],
                TruckChange: null);

            return new RouteOptimizationSolution(
                RouteOptimizationStatus.NoChangeRecommended,
                RouteOptimizationConfidence.Medium,
                [scenario],
                scenario.Reasons,
                scenario.Warnings);
        }

        var reallocation = await FindBestReallocationAsync(
            problem,
            source,
            currentMetrics,
            citiesWithoutCoordinates,
            cancellationToken);
        if (reallocation is not null)
        {
            return new RouteOptimizationSolution(
                RouteOptimizationStatus.Completed,
                reallocation.Confidence,
                [reallocation],
                reallocation.Reasons,
                reallocation.Warnings);
        }

        var truckChange = FindBestTruckChange(problem, source, currentMetrics);
        if (truckChange is not null)
        {
            return new RouteOptimizationSolution(
                RouteOptimizationStatus.Completed,
                truckChange.Confidence,
                [truckChange],
                truckChange.Reasons,
                truckChange.Warnings);
        }

        return new RouteOptimizationSolution(
            RouteOptimizationStatus.NoFeasibleSolution,
            RouteOptimizationConfidence.Insufficient,
            [],
            [new RouteOptimizationReasonDto("NoCompatibleRoute", "Nenhuma rota compatível recebeu cidades sem ficar crítica.")],
            ["Simulação: nenhuma alteração foi aplicada às rotas atuais."]);
    }

    private async Task<RouteOptimizationScenarioCandidate?> FindBestReallocationAsync(
        RouteOptimizationProblem problem,
        OptimizationRoute source,
        RouteOptimizationMetricsDto currentMetrics,
        int citiesWithoutCoordinates,
        CancellationToken cancellationToken)
    {
        var candidates = problem.Routes
            .Where(route => route.RouteId != source.RouteId)
            .Where(route => route.Weekday == source.Weekday)
            .Where(route => route.CapacityKg is > 0 && route.Occupancy.HasValue)
            .Where(route => route.Cities.Any(city => city.Location is not null))
            .OrderBy(route => route.Occupancy)
            .ThenBy(route => route.Name)
            .Take(problem.Constraints.MaximumCandidateRoutes)
            .ToArray();

        RouteOptimizationScenarioCandidate? best = null;
        foreach (var city in source.Cities
            .Where(city => city.Location is not null)
            .OrderByDescending(city => city.LoadKg)
            .ThenBy(city => city.Name))
        {
            foreach (var destination in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceAfterLoad = source.LoadKg - city.LoadKg;
                var destinationAfterLoad = destination.LoadKg + city.LoadKg;
                var sourceAfterOccupancy = sourceAfterLoad / source.CapacityKg!.Value;
                var destinationAfterOccupancy = destinationAfterLoad / destination.CapacityKg!.Value;
                var improvement = source.Occupancy!.Value - sourceAfterOccupancy;
                if (improvement < problem.Constraints.MinimumOccupancyImprovement ||
                    sourceAfterOccupancy > problem.Constraints.MaximumDestinationOccupancy ||
                    destinationAfterOccupancy > problem.Constraints.MaximumDestinationOccupancy)
                {
                    continue;
                }

                var insertionDistance = await EstimateInsertionDistanceAsync(city, destination, cancellationToken);
                if (insertionDistance > problem.Constraints.MaximumEstimatedInsertionDistanceKm)
                {
                    continue;
                }

                var sourceCitiesAfter = source.Cities
                    .Where(item => item.CityId != city.CityId)
                    .OrderBy(item => item.Sequence)
                    .Select(item => item.Name)
                    .ToArray();
                var destinationCitiesAfter = destination.Cities
                    .OrderBy(item => item.Sequence)
                    .Select(item => item.Name)
                    .Append(city.Name)
                    .OrderBy(item => item)
                    .ToArray();

                var reasons = new[]
                {
                    new RouteOptimizationReasonDto(
                        "RelievesOverload",
                        $"A alteração reduz a ocupação da rota de origem de {FormatPercent(source.Occupancy)} para {FormatPercent(sourceAfterOccupancy)}."),
                    new RouteOptimizationReasonDto(
                        "KeepsDestinationHealthy",
                        $"A rota de destino ficaria com {FormatPercent(destinationAfterOccupancy)}, sem mover a sobrecarga para outra rota."),
                    new RouteOptimizationReasonDto(
                        "GeographicProximity",
                        $"A cidade fica a aproximadamente {insertionDistance:N1} km do agrupamento atual da rota de destino.")
                };

                var reallocation = new RouteCityReallocationDto(
                    city.CityId,
                    city.Name,
                    source.RouteId,
                    source.Name,
                    destination.RouteId,
                    destination.Name,
                    city.LoadKg,
                    source.Occupancy,
                    sourceAfterOccupancy,
                    destination.Occupancy,
                    destinationAfterOccupancy,
                    insertionDistance,
                    reasons);

                var proposed = currentMetrics with
                {
                    LoadKg = sourceAfterLoad,
                    Occupancy = sourceAfterOccupancy,
                    OccupancyLevel = RouteOccupancyLevelPolicy.Classify(sourceAfterOccupancy),
                    Cities = sourceCitiesAfter
                };
                var score = improvement * OccupancyPercentScale - insertionDistance * DistanceScoreWeight;
                var warnings = new List<string>
                {
                    "Simulação: nenhuma alteração foi aplicada às rotas atuais.",
                    DistanceWarning(distanceMatrixProvider),
                    $"Destino simulado: {destination.Name} passaria a atender {destinationCitiesAfter.Length} cidade(s)."
                };
                if (citiesWithoutCoordinates > 0)
                {
                    warnings.Add($"{citiesWithoutCoordinates} cidade(s) sem coordenadas foram ignoradas nas tentativas de realocação.");
                }

                var scenario = new RouteOptimizationScenarioCandidate(
                    Score: Math.Round(score, 4, MidpointRounding.AwayFromZero),
                    ActionType: RouteOptimizationActionType.ReallocateCities,
                    Confidence: RouteOptimizationConfidence.Medium,
                    EstimatedDistanceChangeKm: insertionDistance,
                    CurrentMetrics: currentMetrics,
                    ProposedMetrics: proposed,
                    Reasons: reasons,
                    Warnings: warnings,
                    CityReallocations: [reallocation],
                    TruckChange: null);

                if (best is null ||
                    scenario.Score > best.Score ||
                    scenario.Score == best.Score &&
                    string.CompareOrdinal(reallocation.CityName, best.CityReallocations[0].CityName) < 0)
                {
                    best = scenario;
                }
            }
        }

        return best;
    }

    private RouteOptimizationScenarioCandidate? FindBestTruckChange(
        RouteOptimizationProblem problem,
        OptimizationRoute source,
        RouteOptimizationMetricsDto currentMetrics)
    {
        var currentCapacity = source.CapacityKg ?? 0m;
        var truck = problem.TruckModels
            .Where(model => model.TruckModelId != source.TruckModelId)
            .Where(model => model.CapacityKg > currentCapacity)
            .Select(model => new
            {
                Model = model,
                Occupancy = source.LoadKg / model.CapacityKg
            })
            .Where(item => item.Occupancy <= RouteOccupancyLevelPolicy.CriticalMinimumExclusive)
            .OrderBy(item => item.Model.CapacityKg)
            .ThenBy(item => item.Model.Name)
            .FirstOrDefault();

        if (truck is null)
        {
            return null;
        }

        var reasons = new[]
        {
            new RouteOptimizationReasonDto(
                "TruckCapacityFit",
                $"Modelo compatível cadastrado reduz a ocupação de {FormatPercent(source.Occupancy)} para {FormatPercent(truck.Occupancy)}.")
        };
        var proposed = currentMetrics with
        {
            VehicleTypeName = truck.Model.Name,
            CapacityKg = truck.Model.CapacityKg,
            Occupancy = truck.Occupancy,
            OccupancyLevel = RouteOccupancyLevelPolicy.Classify(truck.Occupancy)
        };

        return new RouteOptimizationScenarioCandidate(
            Score: Math.Round((source.Occupancy!.Value - truck.Occupancy) * OccupancyPercentScale, 4, MidpointRounding.AwayFromZero),
            ActionType: RouteOptimizationActionType.ChangeTruck,
            Confidence: RouteOptimizationConfidence.Medium,
            EstimatedDistanceChangeKm: null,
            CurrentMetrics: currentMetrics,
            ProposedMetrics: proposed,
            Reasons: reasons,
            Warnings:
            [
                "Simulação: nenhuma alteração foi aplicada às rotas atuais.",
                "A recomendação considera modelo compatível cadastrado, sem afirmar disponibilidade real de frota."
            ],
            CityReallocations: [],
            TruckChange: new RouteTruckChangeDto(
                source.TruckModelId,
                source.TruckModelName,
                source.CapacityKg,
                truck.Model.TruckModelId,
                truck.Model.Name,
                truck.Model.CapacityKg,
                source.Occupancy,
                truck.Occupancy,
                reasons));
    }

    private async Task<decimal> EstimateInsertionDistanceAsync(
        OptimizationCity city,
        OptimizationRoute destination,
        CancellationToken cancellationToken)
    {
        var distances = new List<decimal>();
        foreach (var destinationCity in destination.Cities.Where(city => city.Location is not null))
        {
            distances.Add(await distanceMatrixProvider.GetDistanceKmAsync(
                city.Location!,
                destinationCity.Location!,
                cancellationToken));
        }

        return distances.Count == 0 ? decimal.MaxValue : distances.Min();
    }

    private static RouteOptimizationSolution Insufficient(string code, string message) =>
        new(
            RouteOptimizationStatus.InsufficientData,
            RouteOptimizationConfidence.Insufficient,
            [],
            [new RouteOptimizationReasonDto(code, message)],
            ["Simulação: nenhuma alteração foi aplicada às rotas atuais."]);

    private static string DistanceWarning(IDistanceMatrixProvider provider) =>
        provider.Method == "OsrmRoadDistance"
            ? "A distância considera percurso rodoviário estimado via OSRM/OpenStreetMap."
            : "A distância é estimada por latitude e longitude, não por percurso rodoviário real.";

    private static RouteOptimizationMetricsDto ToMetrics(OptimizationRoute route) =>
        new(
            route.RouteId,
            route.Name,
            route.TruckModelName,
            route.CapacityKg,
            route.LoadKg,
            route.Occupancy,
            RouteOccupancyLevelPolicy.Classify(route.Occupancy),
            route.Cities.OrderBy(city => city.Sequence).Select(city => city.Name).ToArray());

    private static string FormatPercent(decimal? occupancy) =>
        occupancy.HasValue
            ? Math.Round(
                occupancy.Value * OccupancyPercentScale,
                OccupancyPercentDecimalPlaces,
                MidpointRounding.AwayFromZero).ToString("N1") + "%"
            : "indisponível";
}
