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
}

public sealed class OrToolsDailyRouteOptimizationSolver(IOptions<RouteOptimizationOptions> options) : IDailyRouteOptimizationSolver
{
    private readonly int timeoutSeconds = options.Value.SolverTimeoutSeconds > 0
        ? options.Value.SolverTimeoutSeconds
        : throw new InvalidOperationException("RouteOptimization:SolverTimeoutSeconds deve ser positivo.");

    public RouteOptimizationSolution Solve(RouteOptimizationProblem problem)
    {
        Validate(problem);
        var selection = SelectFleet(problem);
        if (selection is null)
            return new(DailyRouteOptimizationStatuses.Infeasible,
                "Os blocos municipais não cabem na frota existente nem nos tipos adicionais cadastrados.", [], 0, 0);
        var solution = Route(problem, selection);
        DailyRouteOptimizationSolutionValidator.Validate(problem, solution);
        return solution;
    }

    private FleetSelection? SelectFleet(RouteOptimizationProblem problem)
    {
        var candidates = problem.ExistingVehicles.Concat(
            problem.AdditionalVehicleTypes.SelectMany(type =>
                Enumerable.Range(0, problem.Blocks.Count).Select(_ => type with { IsAdditional = true })))
            .ToArray();
        var model = new CpModel();
        var assignments = new BoolVar[problem.Blocks.Count, candidates.Length];
        var used = new BoolVar[candidates.Length];

        for (var vehicle = 0; vehicle < candidates.Length; vehicle++)
        {
            used[vehicle] = model.NewBoolVar($"vehicle_{vehicle}");
            if (!candidates[vehicle].IsAdditional) model.Add(used[vehicle] == 1);
        }
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
            if (candidates[vehicle].IsAdditional)
                model.Add(LinearExpr.Sum(Enumerable.Range(0, problem.Blocks.Count)
                    .Select(block => assignments[block, vehicle])) >= used[vehicle]);
        }

        var additionalIndexes = Enumerable.Range(0, candidates.Length)
            .Where(index => candidates[index].IsAdditional).ToArray();
        var additionalCount = LinearExpr.Sum(additionalIndexes.Select(index => used[index]));
        model.Minimize(additionalCount);
        var solver = NewCpSolver();
        var status = solver.Solve(model);
        if (status == CpSolverStatus.Infeasible) return null;
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
            throw new InvalidOperationException("O OR-Tools não encontrou uma distribuição viável dentro do tempo limite.");
        var feasibleSelection = CaptureSelection(problem, candidates, assignments, used, solver);
        if (status == CpSolverStatus.Feasible) return feasibleSelection;
        var minimumCount = additionalIndexes.Sum(index => solver.Value(used[index]));

        model.Add(additionalCount == minimumCount);
        model.Minimize(LinearExpr.WeightedSum(
            additionalIndexes.Select(index => used[index]),
            additionalIndexes.Select(index => candidates[index].CapacityGrams)));
        solver = NewCpSolver();
        status = solver.Solve(model);
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
            return feasibleSelection;
        return CaptureSelection(problem, candidates, assignments, used, solver);
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
        var fleet = selection.Vehicles;
        var pointIndexByMunicipality = problem.Matrix.Points
            .Select((point, index) => (point, index))
            .Where(item => item.point.Type == OsrmMatrixPointTypes.Municipality)
            .ToDictionary(item => item.point.Id, item => item.index);
        var nodeToMatrix = new[] { 0 }.Concat(problem.Blocks.Select(block => pointIndexByMunicipality[block.MunicipalityId])).ToArray();
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
        var initialAssignment = routing.ReadAssignmentFromRoutes(initialRoutes, true)
            ?? throw new InvalidOperationException("O OR-Tools rejeitou a distribuição viável usada para iniciar a roteirização.");
        var assignment = routing.SolveFromAssignmentWithParameters(initialAssignment, parameters) ?? initialAssignment;

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
            totalDuration += duration;
            vehicles.Add(new(fleet[vehicle], stops, load, distance, duration));
        }
        return new(DailyRouteOptimizationStatuses.Optimized, null, vehicles, totalDistance, totalDuration);
    }

    private sealed record FleetSelection(
        IReadOnlyList<RouteOptimizationVehicleInput> Vehicles,
        IReadOnlyList<IReadOnlyList<int>> InitialRoutes);

    private static long Round(decimal value) => Decimal.ToInt64(decimal.Round(value, 0, MidpointRounding.AwayFromZero));

    private static void Validate(RouteOptimizationProblem problem)
    {
        if (problem.Blocks.Count == 0) throw new ArgumentException("A otimização exige ao menos um bloco municipal.");
        if (problem.ExistingVehicles.Any(vehicle => vehicle.CapacityGrams <= 0) ||
            problem.AdditionalVehicleTypes.Any(vehicle => vehicle.CapacityGrams <= 0))
            throw new ArgumentException("Todos os veículos devem possuir capacidade positiva.");
        if (problem.Blocks.Any(block => block.WeightGrams <= 0))
            throw new ArgumentException("Todos os blocos devem possuir peso positivo.");
        if (problem.Matrix.Points.Count != problem.Blocks.Select(block => block.MunicipalityId).Distinct().Count() + 1)
            throw new ArgumentException("A matriz deve conter depósito e todos os municípios distintos.");
    }
}
