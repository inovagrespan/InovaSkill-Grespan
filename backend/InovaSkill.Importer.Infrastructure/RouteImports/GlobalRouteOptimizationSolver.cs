using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class GlobalRouteOptimizationSolver(IDistanceMatrixProvider distanceMatrixProvider) : IRouteOptimizationSolver
{
    private const decimal DistanceScoreWeight = 0.05m;
    private const decimal PlanDistanceScoreWeight = 0.02m;
    private const decimal CriticalRouteScoreWeight = 1_000m;
    private const decimal OccupancyImprovementScoreWeight = 100m;
    private const decimal OverflowScoreWeight = 500m;
    private const decimal RoutePlanDistanceWeight = 0.08m;
    private const decimal RoutePlanOccupancyWeight = 100m;
    private const decimal RoutePlanOverflowPenalty = 10_000m;
    private const int MaximumBalancedPlanIterations = 300;
    private const int OccupancyPercentScale = 100;

    public bool CanHandle(RouteOptimizationScope scope) => scope == RouteOptimizationScope.AllRoutes;

    public async Task<RouteOptimizationSolution> SolveAsync(
        RouteOptimizationProblem problem,
        CancellationToken cancellationToken)
    {
        if (problem.Scope != RouteOptimizationScope.AllRoutes)
        {
            throw new InvalidOperationException("O solver global exige escopo AllRoutes.");
        }

        if (problem.Routes.Count == 0)
        {
            return Insufficient("MissingRoutes", "Não há rotas válidas no snapshot utilizado pela otimização.");
        }

        if (problem.Routes.Any(route => route.CapacityKg is not > 0 || route.Occupancy is null))
        {
            return Insufficient("MissingCapacity", "Há rotas sem capacidade de caminhão configurada.");
        }

        if (problem.Routes.SelectMany(route => route.Cities).Any(city => city.LoadKg < 0))
        {
            return Insufficient("MissingCityDemand", "Há cidades sem carga válida para calcular o impacto na ocupação.");
        }

        var citiesWithoutCoordinates = problem.Routes
            .SelectMany(route => route.Cities)
            .Count(city => city.Location is null);
        if (problem.Routes.SelectMany(route => route.Cities).All(city => city.Location is null))
        {
            return Insufficient("MissingCoordinates", "Há cidades sem latitude e longitude.");
        }

        var warnings = new List<string>
        {
            "Simulação: nenhuma alteração foi aplicada às rotas atuais.",
            DistanceWarning(distanceMatrixProvider)
        };
        if (citiesWithoutCoordinates > 0)
        {
            warnings.Add($"{citiesWithoutCoordinates} cidade(s) sem coordenadas foram mantidas nas rotas atuais por falta de latitude/longitude.");
        }

        var plannedScenario = await BuildBalancedRoutePlanScenarioAsync(problem, warnings, cancellationToken);
        var emergencyScenario = await BuildEmergencyReallocationScenarioAsync(problem, warnings, cancellationToken);
        if (plannedScenario is not null)
        {
            var scenarios = emergencyScenario is null
                ? [plannedScenario]
                : new[] { plannedScenario, emergencyScenario };
            return new RouteOptimizationSolution(
                RouteOptimizationStatus.Completed,
                RouteOptimizationConfidence.Medium,
                scenarios,
                plannedScenario.Reasons,
                warnings);
        }

        if (emergencyScenario is not null)
        {
            return new RouteOptimizationSolution(
                RouteOptimizationStatus.Completed,
                RouteOptimizationConfidence.Medium,
                [emergencyScenario],
                emergencyScenario.Reasons,
                warnings);
        }

        var hasCriticalRoutes = problem.Routes.Any(route => route.Occupancy > RouteOccupancyLevelPolicy.CriticalMinimumExclusive);
        var status = hasCriticalRoutes
            ? RouteOptimizationStatus.NoFeasibleSolution
            : RouteOptimizationStatus.NoChangeRecommended;
        var code = hasCriticalRoutes
            ? "NoCompatibleRoute"
            : "NoMeaningfulImprovement";
        var message = hasCriticalRoutes
            ? "Nenhum plano global confiável foi encontrado com os caminhões e cidades atuais."
            : "A otimização mais recente não recomenda alterações para as rotas.";

        return new RouteOptimizationSolution(
            status,
            RouteOptimizationConfidence.Medium,
            [Scenario(problem, problem.Routes.Select(route => new MutableRouteState(route)), [], [new RouteOptimizationReasonDto(code, message)], warnings, 0, RouteOptimizationActionType.NoChange)],
            [new RouteOptimizationReasonDto(code, message)],
            warnings);
    }

    private async Task<RouteOptimizationScenarioCandidate?> BuildBalancedRoutePlanScenarioAsync(
        RouteOptimizationProblem problem,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var allCities = problem.Routes
            .SelectMany(route => route.Cities)
            .DistinctBy(city => city.CityId)
            .ToDictionary(city => city.CityId);
        var states = problem.Routes
            .Select(route => new MutableRouteState(route))
            .ToDictionary(route => route.Route.RouteId);
        var reallocations = new List<RouteCityReallocationDto>();
        var iterations = 0;
        while (iterations < MaximumBalancedPlanIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var move = await FindBestBalancedPlanMoveAsync(problem, states.Values, allCities, cancellationToken);
            if (move is null)
            {
                break;
            }

            move.Source.LoadKg -= move.City.LoadKg;
            move.Source.CityIds.Remove(move.City.CityId);
            move.Destination.LoadKg += move.City.LoadKg;
            move.Destination.CityIds.Add(move.City.CityId);
            reallocations.Add(move.ToDto());
            iterations++;
        }

        if (reallocations.Count == 0)
        {
            return null;
        }

        var currentCritical = CountCritical(problem.Routes.Select(route => route.Occupancy));
        var proposedCritical = CountCritical(states.Values.Select(route => (decimal?)route.Occupancy));
        var currentMaxOccupancy = problem.Routes.Max(route => route.Occupancy) ?? 0m;
        var proposedMaxOccupancy = states.Values.Max(route => route.Occupancy);
        var currentOverflow = TotalOverflow(problem.Routes.Select(route => route.Occupancy));
        var proposedOverflow = TotalOverflow(states.Values.Select(route => (decimal?)route.Occupancy));
        if (proposedCritical > currentCritical ||
            proposedMaxOccupancy >= currentMaxOccupancy && proposedOverflow >= currentOverflow)
        {
            return null;
        }

        var reasons = new[]
        {
            new RouteOptimizationReasonDto(
                "BuildBalancedRoutePlan",
                $"Plano principal: redesenhar a distribuição de {reallocations.Count} cidade(s) entre as rotas do mesmo dia, usando as cidades atuais e os caminhões atuais."),
            new RouteOptimizationReasonDto(
                "ReducesGlobalPressure",
                $"O plano reduz rotas críticas de {currentCritical} para {proposedCritical} e a maior ocupação de {FormatPercent(currentMaxOccupancy)} para {FormatPercent(proposedMaxOccupancy)}."),
            new RouteOptimizationReasonDto(
                "KeepsOperationalBase",
                "A recomendação não troca caminhões nem aplica mudanças automaticamente; ela entrega o desenho operacional para conferência.")
        };
        var score =
            (currentCritical - proposedCritical) * CriticalRouteScoreWeight +
            (currentMaxOccupancy - proposedMaxOccupancy) * OccupancyImprovementScoreWeight +
            (currentOverflow - proposedOverflow) * OverflowScoreWeight -
            reallocations.Sum(item => item.EstimatedDistanceChangeKm) * PlanDistanceScoreWeight;

        return Scenario(
            problem,
            states.Values,
            reallocations,
            reasons,
            warnings,
            score,
            RouteOptimizationActionType.BuildBalancedRoutePlan);
    }

    private async Task<PlanMoveCandidate?> FindBestBalancedPlanMoveAsync(
        RouteOptimizationProblem problem,
        IEnumerable<MutableRouteState> states,
        IReadOnlyDictionary<Guid, OptimizationCity> allCities,
        CancellationToken cancellationToken)
    {
        var routeStates = states.ToArray();
        var currentObjective = GlobalObjective(routeStates);
        PlanMoveCandidate? best = null;
        foreach (var source in routeStates
            .Where(route => route.Occupancy > 0)
            .OrderByDescending(route => route.Occupancy)
            .ThenBy(route => route.Route.Name))
        {
            foreach (var city in source.CityIds
                .Select(cityId => allCities[cityId])
                .Where(city => city.Location is not null)
                .OrderByDescending(city => city.LoadKg)
                .ThenBy(city => city.Name))
            {
                var candidates = routeStates
                    .Where(route => route.Route.RouteId != source.Route.RouteId)
                    .Where(route => route.Route.Weekday == source.Route.Weekday)
                    .OrderBy(route => EstimateGeographicDistanceToPlannedRoute(city, route, allCities))
                    .ThenBy(route => route.Occupancy)
                    .ThenBy(route => route.Route.Name)
                    .Take(problem.Constraints.MaximumCandidateRoutes);
                foreach (var destination in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sourceAfterLoad = source.LoadKg - city.LoadKg;
                    var destinationAfterLoad = destination.LoadKg + city.LoadKg;
                    var destinationAfterOccupancy = destinationAfterLoad / destination.Route.CapacityKg!.Value;
                    if (destinationAfterOccupancy > problem.Constraints.MaximumDestinationOccupancy)
                    {
                        continue;
                    }

                    var distance = await EstimateDistanceToPlannedRouteAsync(city, destination, allCities, cancellationToken);
                    if (distance > problem.Constraints.MaximumEstimatedInsertionDistanceKm)
                    {
                        continue;
                    }

                    var objectiveAfter = GlobalObjective(routeStates, source, sourceAfterLoad, destination, destinationAfterLoad);
                    var improvement = currentObjective - objectiveAfter;
                    if (improvement <= 0)
                    {
                        continue;
                    }

                    var candidate = new PlanMoveCandidate(
                        source,
                        destination,
                        city,
                        source.Occupancy,
                        sourceAfterLoad / source.Route.CapacityKg!.Value,
                        destination.Occupancy,
                        destinationAfterOccupancy,
                        distance,
                        improvement);
                    if (best is null || candidate.Score > best.Score)
                    {
                        best = candidate;
                    }
                }
            }
        }

        return best;
    }

    private async Task<MutableRouteState> FindBestPlanRouteAsync(
        OptimizationCity city,
        OptimizationRoute sourceRoute,
        IReadOnlyList<MutableRouteState> candidateRoutes,
        IReadOnlyDictionary<Guid, OptimizationCity> allCities,
        decimal targetOccupancy,
        int maximumCandidateRoutes,
        CancellationToken cancellationToken)
    {
        MutableRouteState? best = null;
        decimal? bestScore = null;
        foreach (var route in candidateRoutes
            .OrderBy(route => EstimateGeographicDistanceToPlannedRoute(city, route, allCities))
            .ThenBy(route => route.Occupancy)
            .ThenBy(route => route.Route.Name)
            .Take(maximumCandidateRoutes))
        {
            var projectedOccupancy = (route.LoadKg + city.LoadKg) / route.Route.CapacityKg!.Value;
            var distance = await EstimateDistanceToPlannedRouteAsync(city, route, allCities, cancellationToken);
            var overloadPenalty = projectedOccupancy > RouteOccupancyLevelPolicy.CriticalMinimumExclusive
                ? (projectedOccupancy - RouteOccupancyLevelPolicy.CriticalMinimumExclusive) * RoutePlanOverflowPenalty
                : 0m;
            var score =
                overloadPenalty +
                Math.Abs(projectedOccupancy - targetOccupancy) * RoutePlanOccupancyWeight +
                distance * RoutePlanDistanceWeight;
            if (route.Route.RouteId == sourceRoute.RouteId)
            {
                score -= 1m;
            }

            if (bestScore is null || score < bestScore.Value)
            {
                best = route;
                bestScore = score;
            }
        }

        return best ?? throw new InvalidOperationException("Nenhuma rota candidata encontrada para o plano global.");
    }

    private static decimal EstimateGeographicDistanceToPlannedRoute(
        OptimizationCity city,
        MutableRouteState destination,
        IReadOnlyDictionary<Guid, OptimizationCity> allCities)
    {
        if (city.Location is null)
        {
            return decimal.MaxValue;
        }

        var distances = destination.CityIds
            .Where(cityId => cityId != city.CityId)
            .Select(cityId => allCities[cityId].Location)
            .Where(location => location is not null)
            .Select(location => EstimateGeographicDistanceKm(city.Location, location!))
            .ToArray();
        if (distances.Length > 0)
        {
            return distances.Min();
        }

        distances = destination.Route.Cities
            .Where(item => item.CityId != city.CityId && item.Location is not null)
            .Select(item => EstimateGeographicDistanceKm(city.Location, item.Location!))
            .ToArray();
        return distances.Length == 0 ? 0m : distances.Min();
    }

    private async Task<RouteOptimizationScenarioCandidate?> BuildEmergencyReallocationScenarioAsync(
        RouteOptimizationProblem problem,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var states = problem.Routes
            .Select(route => new MutableRouteState(route))
            .ToDictionary(route => route.Route.RouteId);
        var reallocations = new List<RouteCityReallocationDto>();

        foreach (var source in states.Values
            .Where(route => route.Occupancy > RouteOccupancyLevelPolicy.CriticalMinimumExclusive)
            .OrderByDescending(route => route.Occupancy)
            .ThenBy(route => route.Route.Name))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = await FindBestMoveAsync(problem, states.Values, source, cancellationToken);
            if (candidate is null) continue;

            source.LoadKg -= candidate.City.LoadKg;
            source.CityIds.Remove(candidate.City.CityId);
            candidate.Destination.LoadKg += candidate.City.LoadKg;
            candidate.Destination.CityIds.Add(candidate.City.CityId);
            reallocations.Add(candidate.ToDto());
        }

        if (reallocations.Count == 0)
        {
            return null;
        }

        var reasons = new[]
        {
            new RouteOptimizationReasonDto(
                "RelievesOverload",
                $"Ação emergencial: realocar {reallocations.Count} cidade(s) para aliviar rotas sobrecarregadas sem redesenhar toda a malha."),
            new RouteOptimizationReasonDto(
                "KeepsDestinationHealthy",
                "As rotas de destino simuladas permanecem dentro do limite crítico conhecido.")
        };
        var score = reallocations.Sum(item =>
            ((item.SourceOccupancyBefore ?? 0m) - (item.SourceOccupancyAfter ?? 0m)) * OccupancyPercentScale -
            item.EstimatedDistanceChangeKm * DistanceScoreWeight);

        return Scenario(
            problem,
            states.Values,
            reallocations,
            reasons,
            warnings,
            score,
            RouteOptimizationActionType.ReallocateCities);
    }

    private async Task<MoveCandidate?> FindBestMoveAsync(
        RouteOptimizationProblem problem,
        IEnumerable<MutableRouteState> states,
        MutableRouteState source,
        CancellationToken cancellationToken)
    {
        MoveCandidate? best = null;
        foreach (var city in source.Route.Cities
            .Where(city => source.CityIds.Contains(city.CityId))
            .Where(city => city.Location is not null)
            .OrderByDescending(city => city.LoadKg)
            .ThenBy(city => city.Name))
        {
            foreach (var destination in states
                .Where(route => route.Route.RouteId != source.Route.RouteId)
                .Where(route => route.Route.Weekday == source.Route.Weekday)
                .OrderBy(route => route.Occupancy)
                .ThenBy(route => route.Route.Name)
                .Take(problem.Constraints.MaximumCandidateRoutes))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceAfterLoad = source.LoadKg - city.LoadKg;
                var destinationAfterLoad = destination.LoadKg + city.LoadKg;
                var sourceAfterOccupancy = sourceAfterLoad / source.Route.CapacityKg!.Value;
                var destinationAfterOccupancy = destinationAfterLoad / destination.Route.CapacityKg!.Value;
                if (source.Occupancy - sourceAfterOccupancy < problem.Constraints.MinimumOccupancyImprovement ||
                    sourceAfterOccupancy > problem.Constraints.MaximumDestinationOccupancy ||
                    destinationAfterOccupancy > problem.Constraints.MaximumDestinationOccupancy)
                {
                    continue;
                }

                var distance = await EstimateInsertionDistanceAsync(city, destination.Route, cancellationToken);
                if (distance > problem.Constraints.MaximumEstimatedInsertionDistanceKm)
                {
                    continue;
                }

                var candidate = new MoveCandidate(
                    source,
                    destination,
                    city,
                    source.Occupancy,
                    sourceAfterOccupancy,
                    destination.Occupancy,
                    destinationAfterOccupancy,
                    distance);
                if (best is null || candidate.Score > best.Score)
                {
                    best = candidate;
                }
            }
        }

        return best;
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

    private async Task<decimal> EstimateDistanceToPlannedRouteAsync(
        OptimizationCity city,
        MutableRouteState destination,
        IReadOnlyDictionary<Guid, OptimizationCity> allCities,
        CancellationToken cancellationToken)
    {
        if (city.Location is null)
        {
            return decimal.MaxValue;
        }

        var distances = new List<decimal>();
        foreach (var destinationCityId in destination.CityIds)
        {
            if (destinationCityId == city.CityId)
            {
                continue;
            }

            var destinationCity = allCities[destinationCityId];
            if (destinationCity.Location is not null)
            {
                distances.Add(await distanceMatrixProvider.GetDistanceKmAsync(
                    city.Location,
                    destinationCity.Location,
                    cancellationToken));
            }
        }

        if (distances.Count > 0)
        {
            return distances.Min();
        }

        foreach (var destinationCity in destination.Route.Cities.Where(item =>
            item.CityId != city.CityId && item.Location is not null))
        {
            distances.Add(await distanceMatrixProvider.GetDistanceKmAsync(
                city.Location,
                destinationCity.Location!,
                cancellationToken));
        }

        return distances.Count == 0 ? 0m : distances.Min();
    }

    private static RouteOptimizationScenarioCandidate Scenario(
        RouteOptimizationProblem problem,
        IEnumerable<MutableRouteState> states,
        IReadOnlyList<RouteCityReallocationDto> reallocations,
        IReadOnlyList<RouteOptimizationReasonDto> reasons,
        IReadOnlyList<string> warnings,
        decimal score,
        RouteOptimizationActionType actionType)
    {
        var currentCritical = problem.Routes.Count(route => RouteOccupancyLevelPolicy.Classify(route.Occupancy) == "critical");
        var proposedCritical = states.Count(route => RouteOccupancyLevelPolicy.Classify(route.Occupancy) == "critical");
        var current = new RouteOptimizationMetricsDto(
            Guid.Empty,
            "Todas as rotas",
            "Múltiplos modelos",
            null,
            problem.Routes.Sum(route => route.LoadKg),
            problem.Routes.Max(route => route.Occupancy),
            $"{currentCritical} crítica(s)",
            problem.Routes.Select(route => route.Name).ToArray());
        var proposed = current with
        {
            Occupancy = states.Max(route => route.Occupancy),
            OccupancyLevel = $"{proposedCritical} crítica(s)"
        };

        return new RouteOptimizationScenarioCandidate(
            Math.Round(score, 4, MidpointRounding.AwayFromZero),
            reallocations.Count > 0 ? actionType : RouteOptimizationActionType.NoChange,
            RouteOptimizationConfidence.Medium,
            reallocations.Count == 0 ? null : reallocations.Sum(item => item.EstimatedDistanceChangeKm),
            current,
            proposed,
            reasons,
            warnings,
            reallocations,
            null);
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

    private static int CountCritical(IEnumerable<decimal?> occupancies) =>
        occupancies.Count(occupancy => RouteOccupancyLevelPolicy.Classify(occupancy) == "critical");

    private static decimal TotalOverflow(IEnumerable<decimal?> occupancies) =>
        occupancies.Sum(occupancy => Math.Max(0m, (occupancy ?? 0m) - RouteOccupancyLevelPolicy.CriticalMinimumExclusive));

    private static decimal GlobalObjective(
        IEnumerable<MutableRouteState> states,
        MutableRouteState? adjustedSource = null,
        decimal? adjustedSourceLoad = null,
        MutableRouteState? adjustedDestination = null,
        decimal? adjustedDestinationLoad = null)
    {
        var occupancies = states.Select(route =>
        {
            var load = route == adjustedSource
                ? adjustedSourceLoad!.Value
                : route == adjustedDestination
                    ? adjustedDestinationLoad!.Value
                    : route.LoadKg;
            return load / route.Route.CapacityKg!.Value;
        }).ToArray();

        return CountCritical(occupancies.Select(occupancy => (decimal?)occupancy)) * CriticalRouteScoreWeight +
            occupancies.Max() * OccupancyImprovementScoreWeight +
            TotalOverflow(occupancies.Select(occupancy => (decimal?)occupancy)) * OverflowScoreWeight;
    }

    private static decimal EstimateGeographicDistanceKm(GeoPoint origin, GeoPoint destination)
    {
        const decimal earthRadiusKm = 6371m;
        var originLatitude = ToRadians(origin.Latitude);
        var destinationLatitude = ToRadians(destination.Latitude);
        var latitudeDelta = ToRadians(destination.Latitude - origin.Latitude);
        var longitudeDelta = ToRadians(destination.Longitude - origin.Longitude);

        var a = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d) +
                Math.Cos(originLatitude) * Math.Cos(destinationLatitude) *
                Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return earthRadiusKm * (decimal)c;
    }

    private static double ToRadians(decimal degrees) => (double)degrees * Math.PI / 180d;

    private static string FormatPercent(decimal occupancy) =>
        Math.Round(occupancy * OccupancyPercentScale, 1, MidpointRounding.AwayFromZero).ToString("N1") + "%";

    private sealed class MutableRouteState(OptimizationRoute route)
    {
        public OptimizationRoute Route { get; } = route;
        public decimal LoadKg { get; set; } = route.LoadKg;
        public HashSet<Guid> CityIds { get; } = route.Cities.Select(city => city.CityId).ToHashSet();
        public decimal Occupancy => LoadKg / Route.CapacityKg!.Value;

        public MutableRouteState(
            OptimizationRoute route,
            decimal loadKg,
            IEnumerable<Guid> cityIds)
            : this(route)
        {
            LoadKg = loadKg;
            CityIds.Clear();
            foreach (var cityId in cityIds)
            {
                CityIds.Add(cityId);
            }
        }
    }

    private sealed record MoveCandidate(
        MutableRouteState Source,
        MutableRouteState Destination,
        OptimizationCity City,
        decimal SourceOccupancyBefore,
        decimal SourceOccupancyAfter,
        decimal DestinationOccupancyBefore,
        decimal DestinationOccupancyAfter,
        decimal EstimatedDistanceChangeKm)
    {
        public decimal Score =>
            (SourceOccupancyBefore - SourceOccupancyAfter) * OccupancyPercentScale -
            EstimatedDistanceChangeKm * DistanceScoreWeight;

        public RouteCityReallocationDto ToDto()
        {
            var reasons = new[]
            {
                new RouteOptimizationReasonDto(
                    "RelievesOverload",
                    $"A alteração reduz a ocupação da rota de origem de {FormatPercent(SourceOccupancyBefore)} para {FormatPercent(SourceOccupancyAfter)}."),
                new RouteOptimizationReasonDto(
                    "KeepsDestinationHealthy",
                    $"A rota de destino ficaria com {FormatPercent(DestinationOccupancyAfter)}, sem mover a sobrecarga para outra rota."),
                new RouteOptimizationReasonDto(
                    "GeographicProximity",
                    $"A cidade fica a aproximadamente {EstimatedDistanceChangeKm:N1} km do agrupamento atual da rota de destino.")
            };

            return new RouteCityReallocationDto(
                City.CityId,
                City.Name,
                Source.Route.RouteId,
                Source.Route.Name,
                Destination.Route.RouteId,
                Destination.Route.Name,
                City.LoadKg,
                SourceOccupancyBefore,
                SourceOccupancyAfter,
                DestinationOccupancyBefore,
                DestinationOccupancyAfter,
                EstimatedDistanceChangeKm,
                reasons);
        }

        private static string FormatPercent(decimal occupancy) =>
            GlobalRouteOptimizationSolver.FormatPercent(occupancy);
    }

    private sealed record PlanMoveCandidate(
        MutableRouteState Source,
        MutableRouteState Destination,
        OptimizationCity City,
        decimal SourceOccupancyBefore,
        decimal SourceOccupancyAfter,
        decimal DestinationOccupancyBefore,
        decimal DestinationOccupancyAfter,
        decimal EstimatedDistanceChangeKm,
        decimal ObjectiveImprovement)
    {
        public decimal Score => ObjectiveImprovement * OccupancyImprovementScoreWeight -
            EstimatedDistanceChangeKm * PlanDistanceScoreWeight;

        public RouteCityReallocationDto ToDto()
        {
            var reasons = new[]
            {
                new RouteOptimizationReasonDto(
                    "BestGlobalFit",
                    $"{City.Name} entra melhor em {Destination.Route.Name} no plano geral, considerando carga, caminhão e proximidade."),
                new RouteOptimizationReasonDto(
                    "TruckCapacityBalance",
                    $"{Destination.Route.Name} ficaria com {FormatPercent(DestinationOccupancyAfter)} de ocupação no cenário recomendado."),
                new RouteOptimizationReasonDto(
                    "SourcePressureAfterPlan",
                    $"{Source.Route.Name} ficaria com {FormatPercent(SourceOccupancyAfter)} após o redesenho completo.")
            };

            return new RouteCityReallocationDto(
                City.CityId,
                City.Name,
                Source.Route.RouteId,
                Source.Route.Name,
                Destination.Route.RouteId,
                Destination.Route.Name,
                City.LoadKg,
                SourceOccupancyBefore,
                SourceOccupancyAfter,
                DestinationOccupancyBefore,
                DestinationOccupancyAfter,
                EstimatedDistanceChangeKm,
                reasons);
        }

        private static string FormatPercent(decimal occupancy) =>
            GlobalRouteOptimizationSolver.FormatPercent(occupancy);
    }
}
