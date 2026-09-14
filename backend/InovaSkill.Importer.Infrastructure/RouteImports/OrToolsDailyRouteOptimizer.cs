using Google.OrTools.ConstraintSolver;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OrToolsDailyRouteOptimizer : IDailyRouteOptimizer
{
    public LegacyDailyRouteOptimizationResult Optimize(DailyRouteOptimizationProblem problem, string matrixSource)
    {
        Validate(problem);
        var rentalTypes = problem.RentalVehicleTypes
            .Where(vehicle => EffectiveCapacity(vehicle.CapacityKg) > 0)
            .OrderBy(vehicle => vehicle.CapacityKg)
            .ToArray();
        if (rentalTypes.Length == 0)
            return Empty(problem, matrixSource, DailyRouteOptimizationStatuses.InsufficientData,
                "Não há tipo de veículo com capacidade válida para simular locação.");
        var ownVehicles = problem.OwnVehicles.Where(vehicle => EffectiveCapacity(vehicle.CapacityKg) > 0).ToArray();
        problem = ExpandOversizedMunicipalLoads(problem, ownVehicles.Concat(rentalTypes)
            .Max(vehicle => EffectiveCapacity(vehicle.CapacityKg)));
        var vehicles = ownVehicles.Concat(Enumerable.Range(0, problem.Stops.Count)
            .SelectMany(index => rentalTypes.Select(type => type with
            {
                RouteId = null,
                RouteName = $"Alugado {type.VehicleType} {index + 1}",
                IsRental = true
            }))).ToArray();
        var manager = new RoutingIndexManager(problem.Points.Count, vehicles.Length, 0);
        var routing = new RoutingModel(manager);
        var duration = Matrix(problem.DurationsSeconds);
        var distance = Matrix(problem.DistancesMeters);
        var demands = new long[problem.Points.Count];
        for (var node = 1; node < problem.Points.Count; node++)
            demands[node] = Scaled(problem.Stops[node - 1].LoadKg);

        for (var vehicleIndex = 0; vehicleIndex < vehicles.Length; vehicleIndex++)
        {
            var efficiency = FuelEfficiency(vehicles[vehicleIndex]);
            var fuelCostCallback = routing.RegisterTransitCallback((from, to) =>
            {
                var meters = distance[manager.IndexToNode(from), manager.IndexToNode(to)];
                var fuelCost = meters / 1_000d / (double)efficiency *
                    (double)problem.DieselPricePerLiter;
                return checked((long)Math.Round(fuelCost * DailyRouteOptimizationPolicy.MonetaryCostScale,
                    MidpointRounding.AwayFromZero));
            });
            routing.SetArcCostEvaluatorOfVehicle(fuelCostCallback, vehicleIndex);
        }
        var demandCallback = routing.RegisterUnaryTransitCallback(index => demands[manager.IndexToNode(index)]);
        routing.AddDimensionWithVehicleCapacity(
            demandCallback,
            0,
            vehicles.Select(vehicle => Scaled(EffectiveCapacity(vehicle.CapacityKg))).ToArray(),
            true,
            "Capacity");
        var distanceCallback = routing.RegisterTransitCallback((from, to) =>
            distance[manager.IndexToNode(from), manager.IndexToNode(to)]);
        routing.AddDimensionWithVehicleCapacity(
            distanceCallback,
            0,
            vehicles.Select(MaximumDistanceMetersWithReserve).ToArray(),
            true,
            "FuelAutonomy");
        for (var vehicle = ownVehicles.Length; vehicle < vehicles.Length; vehicle++)
        {
            var dailyCost = RentalDailyCost(vehicles[vehicle]);
            routing.SetFixedCostOfVehicle(checked((long)Math.Round(
                (double)((dailyCost.Minimum + dailyCost.Maximum) / 2m) * DailyRouteOptimizationPolicy.MonetaryCostScale,
                MidpointRounding.AwayFromZero)), vehicle);
        }

        var parameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
        parameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
        parameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
        parameters.TimeLimit = new Google.Protobuf.WellKnownTypes.Duration
        {
            Seconds = DailyRouteOptimizationPolicy.SolverTimeLimitSeconds
        };
        var solution = routing.SolveWithParameters(parameters);
        if (solution is null)
            return Empty(problem, matrixSource, DailyRouteOptimizationStatuses.Infeasible,
                "O solver não encontrou uma distribuição saudável para todas as cidades.");

        var proposedRoutes = new List<DailyOptimizationRoute>();
        for (var vehicleIndex = 0; vehicleIndex < vehicles.Length; vehicleIndex++)
        {
            var index = routing.Start(vehicleIndex);
            var nodeIndexes = new List<int>();
            long routeDuration = 0;
            long routeDistance = 0;
            while (!routing.IsEnd(index))
            {
                var next = solution.Value(routing.NextVar(index));
                var node = manager.IndexToNode(index);
                if (node != 0) nodeIndexes.Add(node);
                routeDuration += duration[node, manager.IndexToNode(next)];
                routeDistance += distance[node, manager.IndexToNode(next)];
                index = next;
            }
            if (nodeIndexes.Count == 0) continue;
            var stops = nodeIndexes.Select(node => problem.Stops[node - 1]).ToArray();
            var load = stops.Sum(stop => stop.LoadKg);
            var assigned = vehicles[vehicleIndex];
            var efficiency = FuelEfficiency(assigned);
            var tankCapacity = TankCapacity(assigned.VehicleType, assigned.CapacityKg);
            var estimatedFuelLiters = routeDistance / 1_000m / efficiency;
            var remainingFuelLiters = Math.Max(0m,
                tankCapacity * (1m - DailyRouteOptimizationPolicy.FuelTankReserveRate) - estimatedFuelLiters);
            var routeName = assigned.RouteName;
            if (assigned.IsRental)
            {
                var supportingRouteId = stops
                    .GroupBy(stop => stop.OriginalRouteId)
                    .OrderByDescending(group => group.Sum(stop => stop.LoadKg))
                    .ThenBy(group => group.Key)
                    .Select(group => group.Key)
                    .First();
                var supportedRoute = ownVehicles.FirstOrDefault(vehicle => vehicle.RouteId == supportingRouteId);
                routeName = $"{supportedRoute?.RouteName ?? assigned.RouteName} APOIO";
            }
            proposedRoutes.Add(new DailyOptimizationRoute(
                proposedRoutes.Count + 1,
                assigned.RouteId,
                routeName,
                assigned.VehicleType,
                assigned.IsRental,
                load,
                assigned.CapacityKg,
                load / assigned.CapacityKg,
                routeDistance,
                routeDuration,
                decimal.Round(estimatedFuelLiters, 2, MidpointRounding.AwayFromZero),
                tankCapacity,
                decimal.Round(estimatedFuelLiters / tankCapacity * 100m, 1, MidpointRounding.AwayFromZero),
                decimal.Round(remainingFuelLiters * efficiency, 1, MidpointRounding.AwayFromZero),
                assigned.IsRental ? RentalDailyCost(assigned).Minimum : null,
                assigned.IsRental ? RentalDailyCost(assigned).Maximum : null,
                stops));
        }

        var proposed = Metrics(proposedRoutes);
        var currentRoutes = BuildCurrentRoutes(problem, distance, duration);
        var current = Metrics(currentRoutes);
        var currentExceedsPhysicalCapacity = currentRoutes.Any(route => route.Occupancy > 1m);
        var proposedFuel = EstimatedFuelLiters(proposedRoutes);
        var currentFuel = EstimatedFuelLiters(currentRoutes);
        var improved = currentExceedsPhysicalCapacity ||
            (proposed.RentalVehicles <= current.RentalVehicles &&
             (proposedFuel < currentFuel ||
              proposedFuel == currentFuel && proposed.DistanceMeters < current.DistanceMeters));
        return new LegacyDailyRouteOptimizationResult(
            problem.ImportId,
            problem.Weekday,
            improved ? DailyRouteOptimizationStatuses.Optimized : DailyRouteOptimizationStatuses.NoImprovement,
            DailyRouteOptimizationPolicy.RulesVersion,
            matrixSource,
            improved ? null : "A distribuição atual já é igual ou melhor pelos critérios configurados.",
            current,
            improved ? proposed : current,
            improved ? proposedRoutes : currentRoutes);
    }

    private static IReadOnlyList<DailyOptimizationRoute> BuildCurrentRoutes(
        DailyRouteOptimizationProblem problem, long[,] distance, long[,] duration) =>
        problem.OwnVehicles.Select((vehicle, routeIndex) =>
        {
            var stops = problem.Stops.Where(stop => stop.OriginalRouteId == vehicle.RouteId)
                .OrderBy(stop => stop.OriginalSequence).ToArray();
            long meters = 0, seconds = 0;
            var previous = 0;
            foreach (var stop in stops)
            {
                var node = Array.FindIndex(problem.Points.ToArray(), point => point.Id == stop.MunicipalityId);
                meters += distance[previous, node]; seconds += duration[previous, node]; previous = node;
            }
            meters += distance[previous, 0]; seconds += duration[previous, 0];
            var load = stops.Sum(stop => stop.LoadKg);
            return new DailyOptimizationRoute(routeIndex + 1, vehicle.RouteId, vehicle.RouteName,
                vehicle.VehicleType, false, load, vehicle.CapacityKg, load / vehicle.CapacityKg,
                meters, seconds,
                decimal.Round(meters / 1_000m / FuelEfficiency(vehicle), 2, MidpointRounding.AwayFromZero),
                TankCapacity(vehicle.VehicleType, vehicle.CapacityKg),
                decimal.Round(meters / 1_000m / FuelEfficiency(vehicle) /
                    TankCapacity(vehicle.VehicleType, vehicle.CapacityKg) * 100m, 1, MidpointRounding.AwayFromZero),
                decimal.Round(Math.Max(0m, TankCapacity(vehicle.VehicleType, vehicle.CapacityKg) *
                    (1m - DailyRouteOptimizationPolicy.FuelTankReserveRate) - meters / 1_000m / FuelEfficiency(vehicle)) *
                    FuelEfficiency(vehicle), 1, MidpointRounding.AwayFromZero),
                null, null, stops);
        }).Where(route => route.Stops.Count > 0).ToArray();

    private static DailyOptimizationMetrics Metrics(IReadOnlyList<DailyOptimizationRoute> routes) => new(
        routes.Sum(route => route.DistanceMeters),
        routes.Sum(route => route.DurationSeconds),
        routes.Count,
        routes.Count(route => route.IsRental),
        routes.Count == 0 ? 0 : routes.Max(route => route.Occupancy));

    private static LegacyDailyRouteOptimizationResult Empty(DailyRouteOptimizationProblem problem, string source, string status, string message) =>
        new(problem.ImportId, problem.Weekday, status, DailyRouteOptimizationPolicy.RulesVersion, source, message,
            new(0, 0, 0, 0, 0), new(0, 0, 0, 0, 0), []);

    private static long[,] Matrix(IReadOnlyList<IReadOnlyList<decimal>> source)
    {
        var result = new long[source.Count, source.Count];
        for (var row = 0; row < source.Count; row++)
        for (var column = 0; column < source.Count; column++)
            result[row, column] = checked((long)Math.Round(source[row][column], MidpointRounding.AwayFromZero));
        return result;
    }

    private static long MaximumValue(long[,] matrix)
    {
        long maximum = 0;
        for (var row = 0; row < matrix.GetLength(0); row++)
        for (var column = 0; column < matrix.GetLength(1); column++)
            maximum = Math.Max(maximum, matrix[row, column]);
        return maximum;
    }

    private static decimal EffectiveCapacity(decimal capacity) => capacity;

    private static decimal FuelEfficiency(DailyOptimizationVehicle vehicle) =>
        FuelEfficiency(vehicle.VehicleType, vehicle.CapacityKg);

    private static decimal FuelEfficiency(string vehicleType, decimal capacityKg)
    {
        var normalized = vehicleType.ToUpperInvariant();
        if (normalized.Contains("ACCELO") || normalized.Contains("VUC"))
            return DailyRouteOptimizationPolicy.AcceloAverageEfficiencyKmPerLiter;
        if (normalized.Contains("TOCO")) return DailyRouteOptimizationPolicy.TocoAverageEfficiencyKmPerLiter;
        if (normalized.Contains("TRUCK")) return DailyRouteOptimizationPolicy.TruckAverageEfficiencyKmPerLiter;
        if (capacityKg <= 3_500m) return DailyRouteOptimizationPolicy.AcceloAverageEfficiencyKmPerLiter;
        if (capacityKg <= 8_000m) return DailyRouteOptimizationPolicy.TocoAverageEfficiencyKmPerLiter;
        return DailyRouteOptimizationPolicy.TruckAverageEfficiencyKmPerLiter;
    }

    private static decimal EstimatedFuelLiters(IEnumerable<DailyOptimizationRoute> routes) =>
        routes.Sum(route => route.EstimatedFuelLiters);

    private static long MaximumDistanceMetersWithReserve(DailyOptimizationVehicle vehicle) => checked((long)Math.Floor(
        TankCapacity(vehicle.VehicleType, vehicle.CapacityKg) *
        (1m - DailyRouteOptimizationPolicy.FuelTankReserveRate) * FuelEfficiency(vehicle) * 1_000m));

    private static decimal TankCapacity(string vehicleType, decimal capacityKg)
    {
        var normalized = vehicleType.ToUpperInvariant();
        if (normalized.Contains("ACCELO") || normalized.Contains("VUC") || capacityKg <= 3_500m)
            return DailyRouteOptimizationPolicy.AcceloTankCapacityLiters;
        if (normalized.Contains("TOCO") || capacityKg <= 8_000m)
            return DailyRouteOptimizationPolicy.TocoTankCapacityLiters;
        return DailyRouteOptimizationPolicy.TruckTankCapacityLiters;
    }

    private static (decimal Minimum, decimal Maximum) RentalDailyCost(DailyOptimizationVehicle vehicle)
    {
        var normalized = vehicle.VehicleType.ToUpperInvariant();
        if (normalized.Contains("ACCELO") || normalized.Contains("VUC") || vehicle.CapacityKg <= 3_500m)
            return (DailyRouteOptimizationPolicy.AcceloRentalDailyMinimum, DailyRouteOptimizationPolicy.AcceloRentalDailyMaximum);
        if (normalized.Contains("TOCO") || vehicle.CapacityKg <= 8_000m)
            return (DailyRouteOptimizationPolicy.TocoRentalDailyMinimum, DailyRouteOptimizationPolicy.TocoRentalDailyMaximum);
        return (DailyRouteOptimizationPolicy.TruckRentalDailyMinimum, DailyRouteOptimizationPolicy.TruckRentalDailyMaximum);
    }
    private static long Scaled(decimal value) => checked((long)Math.Round(
        value * DailyRouteOptimizationPolicy.WeightScale, MidpointRounding.AwayFromZero));

    private static DailyRouteOptimizationProblem ExpandOversizedMunicipalLoads(
        DailyRouteOptimizationProblem problem,
        decimal maximumEffectiveCapacity)
    {
        var expandedStops = new List<DailyOptimizationStop>();
        var sourceIndexes = new List<int> { 0 };
        for (var stopIndex = 0; stopIndex < problem.Stops.Count; stopIndex++)
        {
            var stop = problem.Stops[stopIndex];
            var chunkCount = Math.Max(1, (int)Math.Ceiling(stop.LoadKg / maximumEffectiveCapacity));
            var chunkLoad = stop.LoadKg / chunkCount;
            var baseDeliveries = stop.Deliveries / chunkCount;
            var deliveryRemainder = stop.Deliveries % chunkCount;
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                expandedStops.Add(stop with
                {
                    LoadKg = chunkLoad,
                    Deliveries = baseDeliveries + (chunkIndex < deliveryRemainder ? 1 : 0)
                });
                sourceIndexes.Add(stopIndex + 1);
            }
        }

        if (expandedStops.Count == problem.Stops.Count)
            return problem;

        var points = sourceIndexes.Select(index => problem.Points[index]).ToArray();
        return problem with
        {
            Points = points,
            Stops = expandedStops,
            DurationsSeconds = ExpandMatrix(problem.DurationsSeconds, sourceIndexes),
            DistancesMeters = ExpandMatrix(problem.DistancesMeters, sourceIndexes)
        };
    }

    private static IReadOnlyList<IReadOnlyList<decimal>> ExpandMatrix(
        IReadOnlyList<IReadOnlyList<decimal>> matrix,
        IReadOnlyList<int> sourceIndexes) =>
        sourceIndexes.Select(row => (IReadOnlyList<decimal>)sourceIndexes
            .Select(column => matrix[row][column]).ToArray()).ToArray();

    private static void Validate(DailyRouteOptimizationProblem problem)
    {
        if (problem.Points.Count != problem.Stops.Count + 1 ||
            problem.DurationsSeconds.Count != problem.Points.Count ||
            problem.DistancesMeters.Count != problem.Points.Count)
            throw new ArgumentException("A matriz e os pontos da otimização são inconsistentes.");
        if (problem.DieselPricePerLiter <= 0)
            throw new ArgumentException("O preço do diesel deve ser maior que zero.");
        if (problem.Stops.Any(stop => stop.LoadKg < 0))
            throw new ArgumentException("A carga municipal não pode ser negativa.");
    }
}
