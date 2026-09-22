using Google.OrTools.ConstraintSolver;
using Google.OrTools.Sat;
using Google.Protobuf.WellKnownTypes;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class RouteOptimizationOptions
{
    public const string SectionName = "RouteOptimization";
    public int SolverTimeoutSeconds { get; set; } = DailyRouteOptimizationPolicy.SolverTimeoutSeconds;
    public int MaximumRouteDurationHours { get; set; } = DailyRouteOptimizationPolicy.MaximumRouteDurationHours;
    public int ServiceTimePerStopMinutes { get; set; } = DailyRouteOptimizationPolicy.DefaultServiceTimePerStopMinutes;
}

public sealed class OrToolsDailyRouteOptimizationSolver(IOptions<RouteOptimizationOptions> options) : IDailyRouteOptimizationSolver
{
    private readonly int timeoutSeconds = options.Value.SolverTimeoutSeconds > 0
        ? options.Value.SolverTimeoutSeconds
        : throw new InvalidOperationException("RouteOptimization:SolverTimeoutSeconds deve ser positivo.");
    private readonly long maximumRouteDurationSeconds = options.Value.MaximumRouteDurationHours is > 0 and <= DailyRouteOptimizationPolicy.MaximumRouteDurationHours
        ? checked((long)options.Value.MaximumRouteDurationHours * DailyRouteOptimizationPolicy.SecondsPerHour)
        : throw new InvalidOperationException($"RouteOptimization:MaximumRouteDurationHours deve estar entre 1 e {DailyRouteOptimizationPolicy.MaximumRouteDurationHours}.");
    private readonly long serviceDurationSeconds = options.Value.ServiceTimePerStopMinutes >= 0
        ? checked((long)options.Value.ServiceTimePerStopMinutes * DailyRouteOptimizationPolicy.SecondsPerMinute)
        : throw new InvalidOperationException("RouteOptimization:ServiceTimePerStopMinutes não pode ser negativo.");

    public RouteOptimizationSolution Solve(RouteOptimizationProblem problem)
    {
        Validate(problem);
        var selection = SelectFleet(problem);
        if (selection is null)
            return new(DailyRouteOptimizationStatuses.Infeasible,
                "As cargas dos clientes não cabem na frota existente nem nos tipos adicionais cadastrados.", [], 0, 0,
                serviceDurationSeconds, maximumRouteDurationSeconds);
        var solution = Route(problem, selection);
        DailyRouteOptimizationSolutionValidator.Validate(problem, solution);
        return solution;
    }

    private FleetSelection? SelectFleet(RouteOptimizationProblem problem)
    {
        if (problem.Blocks.Count > DailyRouteOptimizationPolicy.ExactFleetSelectionMaximumBlocks)
            return SelectFleetHeuristically(problem);

        var candidates = problem.ExistingVehicles.Concat(
            problem.AdditionalVehicleTypes.SelectMany(type =>
                Enumerable.Range(0, problem.Blocks.Count).Select(_ => type with { IsAdditional = true })))
            .ToArray();
        var model = new CpModel();
        var assignments = new BoolVar[problem.Blocks.Count, candidates.Length];
        var used = new BoolVar[candidates.Length];

        for (var vehicle = 0; vehicle < candidates.Length; vehicle++)
            used[vehicle] = model.NewBoolVar($"vehicle_{vehicle}");
        foreach (var group in Enumerable.Range(problem.ExistingVehicles.Count, candidates.Length - problem.ExistingVehicles.Count)
                     .GroupBy(index => candidates[index].VehicleTypeId))
        {
            var equivalentVehicles = group.ToArray();
            for (var index = 1; index < equivalentVehicles.Length; index++)
                model.Add(used[equivalentVehicles[index - 1]] >= used[equivalentVehicles[index]]);
        }
        for (var block = 0; block < problem.Blocks.Count; block++)
        {
            for (var vehicle = 0; vehicle < candidates.Length; vehicle++)
            {
                assignments[block, vehicle] = model.NewBoolVar($"block_{block}_vehicle_{vehicle}");
                model.Add(assignments[block, vehicle] <= used[vehicle]);
            }
            model.Add(LinearExpr.Sum(Enumerable.Range(0, candidates.Length)
                .Select(vehicle => assignments[block, vehicle])) == 1);
        }
        for (var vehicle = 0; vehicle < candidates.Length; vehicle++)
        {
            model.Add(LinearExpr.WeightedSum(
                Enumerable.Range(0, problem.Blocks.Count).Select(block => assignments[block, vehicle]),
                problem.Blocks.Select(block => block.WeightGrams)) <= candidates[vehicle].CapacityGrams);
            if (serviceDurationSeconds > 0)
                model.Add(LinearExpr.Sum(Enumerable.Range(0, problem.Blocks.Count)
                    .Select(block => assignments[block, vehicle])) <= maximumRouteDurationSeconds / serviceDurationSeconds);
            if (candidates[vehicle].IsAdditional)
                model.Add(LinearExpr.Sum(Enumerable.Range(0, problem.Blocks.Count)
                    .Select(block => assignments[block, vehicle])) >= used[vehicle]);
        }

        var allIndexes = Enumerable.Range(0, candidates.Length).ToArray();
        var additionalIndexes = allIndexes.Where(index => candidates[index].IsAdditional).ToArray();
        var totalUsed = LinearExpr.Sum(allIndexes.Select(index => used[index]));
        var additionalCount = LinearExpr.Sum(additionalIndexes.Select(index => used[index]));
        model.Minimize(totalUsed);
        var solver = NewCpSolver();
        var status = solver.Solve(model);
        if (status == CpSolverStatus.Infeasible) return null;
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
        {
            // O limite de tempo não transforma um problema viável em erro de negócio.
            // Usa-se a mesma alocação determinística de grande volume para manter a
            // execução utilizável e preservar a garantia de capacidade.
            return SelectFleetHeuristically(problem);
        }
        var feasibleSelection = CaptureSelection(problem, candidates, assignments, used, solver);
        if (status == CpSolverStatus.Feasible)
        {
            var deterministicSelection = SelectFleetHeuristically(problem);
            return PreferExistingFleet(feasibleSelection, deterministicSelection);
        }
        var minimumUsed = allIndexes.Sum(index => solver.Value(used[index]));

        model.Add(totalUsed == minimumUsed);
        model.Minimize(additionalCount);
        solver = NewCpSolver();
        status = solver.Solve(model);
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
            return PreferExistingFleet(feasibleSelection, SelectFleetHeuristically(problem));
        var minimumAdditional = additionalIndexes.Sum(index => solver.Value(used[index]));

        model.Add(additionalCount == minimumAdditional);
        model.Minimize(LinearExpr.WeightedSum(
            additionalIndexes.Select(index => used[index]),
            additionalIndexes.Select(index => candidates[index].CapacityGrams)));
        solver = NewCpSolver();
        status = solver.Solve(model);
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
            return PreferExistingFleet(feasibleSelection, SelectFleetHeuristically(problem));
        return PreferExistingFleet(
            CaptureSelection(problem, candidates, assignments, used, solver),
            SelectFleetHeuristically(problem));
    }

    private static FleetSelection PreferExistingFleet(
        FleetSelection exactSelection,
        FleetSelection? deterministicSelection)
    {
        if (deterministicSelection is null)
            return exactSelection;
        if (deterministicSelection.Vehicles.Count < exactSelection.Vehicles.Count)
            return deterministicSelection;
        var exactAdditionalCount = exactSelection.Vehicles.Count(vehicle => vehicle.IsAdditional);
        var deterministicAdditionalCount = deterministicSelection.Vehicles.Count(vehicle => vehicle.IsAdditional);
        if (deterministicAdditionalCount < exactAdditionalCount)
            return deterministicSelection;
        if (deterministicAdditionalCount > exactAdditionalCount)
            return exactSelection;
        var exactAdditionalCapacity = exactSelection.Vehicles
            .Where(vehicle => vehicle.IsAdditional).Sum(vehicle => vehicle.CapacityGrams);
        var deterministicAdditionalCapacity = deterministicSelection.Vehicles
            .Where(vehicle => vehicle.IsAdditional).Sum(vehicle => vehicle.CapacityGrams);
        return deterministicAdditionalCapacity < exactAdditionalCapacity
            ? deterministicSelection
            : exactSelection;
    }

    private FleetSelection? SelectFleetHeuristically(RouteOptimizationProblem problem)
    {
        var availableExisting = problem.ExistingVehicles
            .OrderByDescending(vehicle => vehicle.CapacityGrams)
            .ThenBy(vehicle => vehicle.VehicleTypeName, StringComparer.Ordinal)
            .ToList();
        var additionalTypes = problem.AdditionalVehicleTypes
            .OrderByDescending(vehicle => vehicle.CapacityGrams)
            .ThenBy(vehicle => vehicle.VehicleTypeName, StringComparer.Ordinal)
            .ToArray();
        var vehicles = new List<RouteOptimizationVehicleInput>();
        var routes = new List<List<int>>();
        var remainingCapacity = new List<long>();
        foreach (var blockIndex in Enumerable.Range(0, problem.Blocks.Count)
                     .OrderByDescending(index => problem.Blocks[index].WeightGrams)
                     .ThenBy(index => problem.Blocks[index].CustomerId))
        {
            var weight = problem.Blocks[blockIndex].WeightGrams;
            var maximumStopsPerVehicle = serviceDurationSeconds > 0
                ? maximumRouteDurationSeconds / serviceDurationSeconds
                : long.MaxValue;
            var vehicleIndex = Enumerable.Range(0, vehicles.Count)
                .Where(index => remainingCapacity[index] >= weight && routes[index].Count < maximumStopsPerVehicle)
                .OrderBy(index => remainingCapacity[index] - weight)
                .ThenBy(index => index)
                .Select(index => (int?)index)
                .FirstOrDefault();
            if (!vehicleIndex.HasValue)
            {
                var nextExisting = availableExisting.FirstOrDefault(vehicle => vehicle.CapacityGrams >= weight);
                var nextAdditional = additionalTypes.FirstOrDefault(vehicle => vehicle.CapacityGrams >= weight);
                var next = nextExisting is null ? nextAdditional :
                    nextAdditional is null || nextExisting.CapacityGrams >= nextAdditional.CapacityGrams
                        ? nextExisting : nextAdditional with { IsAdditional = true };
                if (next is null) return null;
                if (nextExisting is not null && next == nextExisting)
                    availableExisting.Remove(nextExisting);
                vehicles.Add(next.IsAdditional ? next with { IsAdditional = true } : next);
                routes.Add([]);
                remainingCapacity.Add(next.CapacityGrams);
                vehicleIndex = vehicles.Count - 1;
            }
            routes[vehicleIndex.Value].Add(blockIndex);
            remainingCapacity[vehicleIndex.Value] -= weight;
        }
        var active = routes.Select((route, index) => new
            {
                Vehicle = vehicles[index],
                Blocks = (IReadOnlyList<int>)route.OrderBy(blockIndex => blockIndex).ToArray()
            })
            .Where(item => item.Blocks.Count > 0)
            .ToArray();
        return new FleetSelection(
            active.Select(item => item.Vehicle).ToArray(),
            active.Select(item => item.Blocks).ToArray());
    }

    private static FleetSelection CaptureSelection(
        RouteOptimizationProblem problem,
        IReadOnlyList<RouteOptimizationVehicleInput> candidates,
        BoolVar[,] assignments,
        IReadOnlyList<BoolVar> used,
        CpSolver solver)
    {
        var selectedCandidateIndexes = Enumerable.Range(0, candidates.Count)
            .Where(index => !candidates[index].IsAdditional || solver.BooleanValue(used[index]))
            .ToArray();
        var selectedVehicles = selectedCandidateIndexes.Select(index => candidates[index]).ToArray();
        var initialRoutes = selectedCandidateIndexes.Select(candidateIndex =>
            Enumerable.Range(0, problem.Blocks.Count)
                .Where(block => solver.BooleanValue(assignments[block, candidateIndex]))
                .OrderBy(block => block)
                .ToArray()).ToArray();
        return new(selectedVehicles, initialRoutes);
    }

    private CpSolver NewCpSolver()
    {
        var solver = new CpSolver();
        solver.StringParameters = $"max_time_in_seconds:{timeoutSeconds} num_search_workers:1 random_seed:1";
        return solver;
    }

    private RouteOptimizationSolution Route(
        RouteOptimizationProblem problem,
        FleetSelection selection)
    {
        var currentSelection = selection;
        // A tentativa inicial usa a frota mínima que respeita capacidade e
        // atendimento. No máximo três reparos são permitidos: tentativas
        // ilimitadas repetiam buscas de 30s e deixavam o job sem diagnóstico.
        var maximumRepairAttempts = serviceDurationSeconds > 0
            ? DailyRouteOptimizationPolicy.MaximumRouteRepairAttempts
            : problem.Blocks.Count;
        for (var attempt = 0; attempt <= maximumRepairAttempts; attempt++)
        {
            var solution = TryRoute(problem, currentSelection);
            if (solution is not null) return solution;
            if (attempt == 0)
            {
                // A seleção por capacidade é deliberadamente econômica, mas a
                // geometria pode exigir a frota original completa para manter
                // cada jornada abaixo de 10h. Reaproveita-se essa frota antes
                // de introduzir veículos adicionais.
                var selectedRouteIds = currentSelection.Vehicles
                    .Where(vehicle => vehicle.SourceRouteId.HasValue)
                    .Select(vehicle => vehicle.SourceRouteId!.Value)
                    .ToHashSet();
                var missingExisting = problem.ExistingVehicles
                    .Where(vehicle => vehicle.SourceRouteId.HasValue &&
                        !selectedRouteIds.Contains(vehicle.SourceRouteId.Value))
                    .ToArray();
                if (missingExisting.Length > 0)
                {
                    currentSelection = new FleetSelection(
                        currentSelection.Vehicles.Concat(missingExisting).ToArray(),
                        currentSelection.InitialRoutes.Concat(missingExisting.Select(_ => (IReadOnlyList<int>)Array.Empty<int>())).ToArray());
                    continue;
                }
            }
            var additional = problem.AdditionalVehicleTypes
                .OrderByDescending(vehicle => vehicle.CapacityGrams)
                .ThenBy(vehicle => vehicle.VehicleTypeName, StringComparer.Ordinal)
                .FirstOrDefault();
            if (additional is null) break;
            currentSelection = new FleetSelection(
                currentSelection.Vehicles.Append(additional with { IsAdditional = true }).ToArray(),
                currentSelection.InitialRoutes.Append(Array.Empty<int>()).ToArray());
        }
        return new(DailyRouteOptimizationStatuses.Infeasible,
            $"Não foi possível distribuir as paradas em rotas de até {maximumRouteDurationSeconds / DailyRouteOptimizationPolicy.SecondsPerHour} horas.",
            [], 0, 0, serviceDurationSeconds, maximumRouteDurationSeconds);
    }

    private RouteOptimizationSolution? TryRoute(
        RouteOptimizationProblem problem,
        FleetSelection selection)
    {
        var fleet = selection.Vehicles;
        var pointIndexByLocation = problem.Matrix.Points
            .Select((point, index) => (point, index))
            .Where(item => item.point.Type != OsrmMatrixPointTypes.Depot)
            .ToDictionary(item => item.point.Id, item => item.index);
        var nodeToMatrix = new[] { 0 }.Concat(problem.Blocks.Select(block =>
            pointIndexByLocation[block.LocationId])).ToArray();
        var manager = new RoutingIndexManager(problem.Blocks.Count + 1, fleet.Count, 0);
        var routing = new RoutingModel(manager);
        var maximumDuration = problem.Matrix.DurationsSeconds.SelectMany(row => row).Max();
        var durationTieBreakerBase = checked((Round(maximumDuration) * (problem.Blocks.Count + fleet.Count)) + 1);
        var distanceCallback = routing.RegisterTransitCallback((from, to) =>
        {
            var fromNode = manager.IndexToNode(from);
            var toNode = manager.IndexToNode(to);
            var fromMatrix = nodeToMatrix[fromNode];
            var toMatrix = nodeToMatrix[toNode];
            return checked(Round(problem.Matrix.DistancesMeters[fromMatrix][toMatrix]) * durationTieBreakerBase +
                           Round(problem.Matrix.DurationsSeconds[fromMatrix][toMatrix]));
        });
        routing.SetArcCostEvaluatorOfAllVehicles(distanceCallback);
        var durationCallback = routing.RegisterTransitCallback((from, to) =>
        {
            var fromNode = manager.IndexToNode(from);
            var toNode = manager.IndexToNode(to);
            var fromMatrix = nodeToMatrix[fromNode];
            var toMatrix = nodeToMatrix[toNode];
            var travelDuration = Round(problem.Matrix.DurationsSeconds[fromMatrix][toMatrix]);
            return checked(travelDuration + (fromNode == 0 ? 0 : serviceDurationSeconds));
        });
        routing.AddDimension(durationCallback, 0, maximumRouteDurationSeconds, true, "WorkDuration");
        var workDurationDimension = routing.GetDimensionOrDie("WorkDuration");
        for (var vehicle = 0; vehicle < fleet.Count; vehicle++)
        {
            // A rota até 8h é preferida, sem transformar a preferência em uma
            // restrição que force veículos ociosos. O limite de 10h continua rígido.
            workDurationDimension.SetCumulVarSoftUpperBound(
                routing.End(vehicle),
                DailyRouteOptimizationPolicy.PreferredRouteDurationSeconds,
                DailyRouteOptimizationPolicy.PreferredDurationPenaltyPerSecond);
        }
        var maximumArcCost = checked(Round(problem.Matrix.DistancesMeters.SelectMany(row => row).Max()) *
            durationTieBreakerBase + Round(maximumDuration));
        var additionalVehicleFixedCost = checked(maximumArcCost * (problem.Blocks.Count + fleet.Count + 1L));
        for (var vehicle = 0; vehicle < fleet.Count; vehicle++)
        {
            if (fleet[vehicle].IsAdditional)
                routing.SetFixedCostOfVehicle(additionalVehicleFixedCost, vehicle);
        }
        var demandCallback = routing.RegisterUnaryTransitCallback(index =>
        {
            var node = manager.IndexToNode(index);
            return node == 0 ? 0 : problem.Blocks[node - 1].WeightGrams;
        });
        routing.AddDimensionWithVehicleCapacity(
            demandCallback, 0, fleet.Select(vehicle => vehicle.CapacityGrams).ToArray(), true, "Capacity");
        var parameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
        parameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
        parameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GreedyDescent;
        parameters.TimeLimit = Duration.FromTimeSpan(TimeSpan.FromSeconds(timeoutSeconds));
        parameters.LogSearch = false;
        var initialRoutes = selection.InitialRoutes
            .Select(route => route.Select(blockIndex => (long)blockIndex + 1).ToArray())
            .ToArray();
        var initialAssignment = routing.ReadAssignmentFromRoutes(initialRoutes, true);
        var assignment = initialAssignment is null
            ? routing.SolveWithParameters(parameters)
            : routing.SolveFromAssignmentWithParameters(initialAssignment, parameters) ??
                routing.SolveWithParameters(parameters);
        if (assignment is null) return null;

        var vehicles = new List<RouteOptimizationVehicleSolution>(fleet.Count);
        long totalDistance = 0;
        long totalDuration = 0;
        for (var vehicle = 0; vehicle < fleet.Count; vehicle++)
        {
            var index = routing.Start(vehicle);
            var stops = new List<RouteOptimizationStopSolution>();
            long load = 0, distance = 0, duration = 0;
            while (!routing.IsEnd(index))
            {
                var next = assignment.Value(routing.NextVar(index));
                var fromNode = manager.IndexToNode(index);
                var toNode = manager.IndexToNode(next);
                var fromMatrix = nodeToMatrix[fromNode];
                var toMatrix = nodeToMatrix[toNode];
                var legDistance = Round(problem.Matrix.DistancesMeters[fromMatrix][toMatrix]);
                var legDuration = Round(problem.Matrix.DurationsSeconds[fromMatrix][toMatrix]);
                distance += legDistance;
                duration += legDuration;
                if (toNode != 0)
                {
                    load += problem.Blocks[toNode - 1].WeightGrams;
                    stops.Add(new(toNode - 1, legDistance, legDuration));
                }
                index = next;
            }
            totalDistance += distance;
            duration = checked(duration + stops.Count * serviceDurationSeconds);
            totalDuration += duration;
            vehicles.Add(new(fleet[vehicle], stops, load, distance, duration));
        }
        return new(DailyRouteOptimizationStatuses.Optimized, null, vehicles, totalDistance, totalDuration,
            serviceDurationSeconds, maximumRouteDurationSeconds);
    }

    private sealed record FleetSelection(
        IReadOnlyList<RouteOptimizationVehicleInput> Vehicles,
        IReadOnlyList<IReadOnlyList<int>> InitialRoutes);

    private static long Round(decimal value) => Decimal.ToInt64(decimal.Round(value, 0, MidpointRounding.AwayFromZero));

    private static void Validate(RouteOptimizationProblem problem)
    {
        if (problem.Blocks.Count == 0) throw new ArgumentException("A otimização exige ao menos uma parada de cliente.");
        if (problem.ExistingVehicles.Any(vehicle => vehicle.CapacityGrams <= 0) ||
            problem.AdditionalVehicleTypes.Any(vehicle => vehicle.CapacityGrams <= 0))
            throw new ArgumentException("Todos os veículos devem possuir capacidade positiva.");
        if (problem.Blocks.Any(block => block.WeightGrams <= 0))
            throw new ArgumentException("Todos os blocos devem possuir peso positivo.");
        if (problem.Matrix.Points.Count != problem.Blocks.Select(block => block.LocationId).Distinct().Count() + 1)
            throw new ArgumentException("A matriz deve conter o depósito e todas as localizações distintas.");
    }
}
