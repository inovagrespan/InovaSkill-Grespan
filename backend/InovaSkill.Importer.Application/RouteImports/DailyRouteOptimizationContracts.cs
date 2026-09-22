using System.Text.Json;

namespace InovaSkill.Importer.Application.RouteImports;

public static class DailyRouteOptimizationStatuses
{
    public const string Optimized = "Optimized";
    public const string NoImprovement = "NoImprovement";
    public const string Infeasible = "Infeasible";
    public const string InsufficientData = "InsufficientData";
}

public static class DailyRouteOptimizationPolicy
{
    public const int WeightScale = 1000;
    public const int SolverTimeoutSeconds = 30;
    public const int ExactFleetSelectionMaximumBlocks = 200;
    public const int OccupancyScale = 100;
    public const int ContractVersion = 2;
    public const int MonetaryCostScale = 100;
    public const int SolverTimeLimitSeconds = 5;
    public const int MaximumRouteRepairAttempts = 3;
    // A jornada ideal é de até 8h, mas uma rota pode utilizar até 15h quando
    // isso evita uma frota artificialmente fragmentada. O limite de 15h é rígido.
    public const int PreferredRouteDurationHours = 8;
    public const int MaximumRouteDurationHours = 15;
    public const int DefaultServiceTimePerStopMinutes = 15;
    public const long SecondsPerMinute = 60;
    public const long SecondsPerHour = 60 * SecondsPerMinute;
    public const long PreferredRouteDurationSeconds = PreferredRouteDurationHours * SecondsPerHour;
    public const long MaximumRouteDurationSeconds = MaximumRouteDurationHours * SecondsPerHour;
    public const long PreferredDurationPenaltyPerSecond = 1000;
    public const decimal FuelTankReserveRate = 0.10m;
    public const decimal AcceloAverageEfficiencyKmPerLiter = 6.25m;
    public const decimal TocoAverageEfficiencyKmPerLiter = 4.15m;
    public const decimal TruckAverageEfficiencyKmPerLiter = 3.60m;
    public const decimal AcceloTankCapacityLiters = 200m;
    public const decimal TocoTankCapacityLiters = 300m;
    public const decimal TruckTankCapacityLiters = 300m;
    public const decimal AcceloRentalDailyMinimum = 400m;
    public const decimal AcceloRentalDailyMaximum = 600m;
    public const decimal TocoRentalDailyMinimum = 650m;
    public const decimal TocoRentalDailyMaximum = 900m;
    public const decimal TruckRentalDailyMinimum = 900m;
    public const decimal TruckRentalDailyMaximum = 1_300m;
    public const string RulesVersion = "daily-v9-exact-customer-locations-max-15h";
}

public static class DailyRouteOptimizationWeekdays
{
    private static readonly HashSet<string> Supported =
    ["MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY"];

    public static IReadOnlyList<string>? Read(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("weekdays", out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("$.weekdays deve ser uma lista de dias.");
        var weekdays = value.EnumerateArray().Select(item =>
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                throw new ArgumentException("$.weekdays contém um dia inválido.");
            return item.GetString()!.Trim().ToUpperInvariant();
        }).Distinct(StringComparer.Ordinal).ToArray();
        if (weekdays.Length == 0 || weekdays.Any(item => !Supported.Contains(item)))
            throw new ArgumentException("$.weekdays contém um dia inválido.");
        return weekdays;
    }
}

public sealed record RouteOptimizationBlock(
    Guid LocationId,
    Guid MunicipalityId,
    Guid CustomerId,
    string Name,
    long WeightGrams)
{
    public RouteOptimizationBlock(Guid municipalityId, string name, long weightGrams)
        : this(municipalityId, municipalityId, municipalityId, name, weightGrams)
    {
    }
}
public sealed record RouteOptimizationVehicleInput(
    Guid? SourceRouteId, Guid VehicleTypeId, string VehicleTypeName, long CapacityGrams, bool IsAdditional);
public sealed record RouteOptimizationProblem(
    string Weekday,
    IReadOnlyList<RouteOptimizationBlock> Blocks,
    IReadOnlyList<RouteOptimizationVehicleInput> ExistingVehicles,
    IReadOnlyList<RouteOptimizationVehicleInput> AdditionalVehicleTypes,
    OsrmTableResult Matrix);
public sealed record RouteOptimizationStopSolution(int BlockIndex, long DistanceFromPreviousMeters, long DurationFromPreviousSeconds);
public sealed record RouteOptimizationVehicleSolution(
    RouteOptimizationVehicleInput Vehicle,
    IReadOnlyList<RouteOptimizationStopSolution> Stops,
    long LoadGrams,
    long DistanceMeters,
    long DurationSeconds);
public sealed record RouteOptimizationSolution(
    string Status,
    string? Reason,
    IReadOnlyList<RouteOptimizationVehicleSolution> Vehicles,
    long TotalDistanceMeters,
    long TotalDurationSeconds,
    long ServiceDurationSeconds = 0,
    long MaximumRouteDurationSeconds = long.MaxValue);

public interface IDailyRouteOptimizationSolver
{
    RouteOptimizationSolution Solve(RouteOptimizationProblem problem);
}

public sealed record DailyOptimizationVehicle(
    Guid? RouteId, Guid VehicleTypeId, string RouteName, string VehicleType,
    decimal CapacityKg, bool IsRental);

public sealed record DailyOptimizationStop(
    Guid MunicipalityId, string MunicipalityName, decimal LoadKg, int Deliveries,
    Guid OriginalRouteId, int OriginalSequence);

public sealed record DailyOptimizationRoute(
    int Sequence, Guid? OriginalRouteId, string Name, string VehicleType, bool IsRental,
    decimal LoadKg, decimal CapacityKg, decimal Occupancy, decimal DistanceMeters,
    decimal DurationSeconds, decimal EstimatedFuelLiters, decimal TankCapacityLiters,
    decimal TankUsagePercent, decimal RemainingAutonomyKm,
    decimal? RentalDailyCostMinimum, decimal? RentalDailyCostMaximum,
    IReadOnlyList<DailyOptimizationStop> Stops);

public sealed record DailyOptimizationMetrics(
    decimal DistanceMeters, decimal DurationSeconds, int VehiclesUsed,
    int RentalVehicles, decimal MaximumOccupancy);

public sealed record LegacyDailyRouteOptimizationResult(
    Guid ImportId, string Weekday, string Status, string RulesVersion, string MatrixSource,
    string? Message, DailyOptimizationMetrics Current, DailyOptimizationMetrics Proposed,
    IReadOnlyList<DailyOptimizationRoute> Routes);

public sealed record DailyRouteOptimizationProblem(
    Guid ImportId, string Weekday, decimal DieselPricePerLiter,
    IReadOnlyList<OsrmMatrixPoint> Points,
    IReadOnlyList<IReadOnlyList<decimal>> DurationsSeconds,
    IReadOnlyList<IReadOnlyList<decimal>> DistancesMeters,
    IReadOnlyList<DailyOptimizationStop> Stops,
    IReadOnlyList<DailyOptimizationVehicle> OwnVehicles,
    IReadOnlyList<DailyOptimizationVehicle> RentalVehicleTypes);

public interface IDailyRouteOptimizer
{
    LegacyDailyRouteOptimizationResult Optimize(DailyRouteOptimizationProblem problem, string matrixSource);
}

public static class DailyRouteOptimizationSolutionValidator
{
    public static void Validate(RouteOptimizationProblem problem, RouteOptimizationSolution solution)
    {
        if (solution.Status is not (DailyRouteOptimizationStatuses.Optimized or
            DailyRouteOptimizationStatuses.NoImprovement or
            DailyRouteOptimizationStatuses.Infeasible or
            DailyRouteOptimizationStatuses.InsufficientData))
            throw new InvalidOperationException("A solução possui um estado desconhecido.");
        if (solution.Status != DailyRouteOptimizationStatuses.Optimized)
        {
            if (solution.Vehicles.Count != 0)
                throw new InvalidOperationException("Resultados não otimizados não podem carregar uma distribuição alternativa.");
            if (string.IsNullOrWhiteSpace(solution.Reason))
                throw new InvalidOperationException("Resultados não otimizados devem explicar por que a rota real foi mantida.");
            return;
        }

        var visitedBlocks = new int[problem.Blocks.Count];
        var matrixPointByLocation = problem.Matrix.Points
            .Select((point, index) => (point, index))
            .Where(item => item.point.Type != OsrmMatrixPointTypes.Depot)
            .ToDictionary(item => item.point.Id, item => item.index);
        long totalLoad = 0;
        long totalDistance = 0;
        long totalDuration = 0;
        foreach (var vehicle in solution.Vehicles)
        {
            if (vehicle.Vehicle.CapacityGrams <= 0 || vehicle.LoadGrams < 0 ||
                vehicle.LoadGrams > vehicle.Vehicle.CapacityGrams)
                throw new InvalidOperationException("A solução excede a capacidade de um veículo.");
            if (vehicle.DistanceMeters < 0 || vehicle.DurationSeconds < 0)
                throw new InvalidOperationException("A solução contém distância ou duração negativa.");
            if (vehicle.DurationSeconds > solution.MaximumRouteDurationSeconds)
                throw new InvalidOperationException("A solução excede o limite de duração da rota.");

            long calculatedLoad = 0;
            long calculatedDistance = 0;
            long calculatedDuration = 0;
            var previousMatrixPoint = 0;
            foreach (var stop in vehicle.Stops)
            {
                if (stop.BlockIndex < 0 || stop.BlockIndex >= problem.Blocks.Count)
                    throw new InvalidOperationException("A solução referencia um bloco inexistente.");
                if (stop.DistanceFromPreviousMeters < 0 || stop.DurationFromPreviousSeconds < 0)
                    throw new InvalidOperationException("A solução contém trecho negativo.");
                visitedBlocks[stop.BlockIndex]++;
                calculatedLoad = checked(calculatedLoad + problem.Blocks[stop.BlockIndex].WeightGrams);
                var nextMatrixPoint = matrixPointByLocation[problem.Blocks[stop.BlockIndex].LocationId];
                var expectedLegDistance = Round(problem.Matrix.DistancesMeters[previousMatrixPoint][nextMatrixPoint]);
                var expectedLegDuration = Round(problem.Matrix.DurationsSeconds[previousMatrixPoint][nextMatrixPoint]);
                if (stop.DistanceFromPreviousMeters != expectedLegDistance ||
                    stop.DurationFromPreviousSeconds != expectedLegDuration)
                    throw new InvalidOperationException("As métricas de um trecho divergem da matriz diária.");
                calculatedDistance = checked(calculatedDistance + expectedLegDistance);
                calculatedDuration = checked(calculatedDuration + expectedLegDuration);
                previousMatrixPoint = nextMatrixPoint;
            }
            calculatedDistance = checked(calculatedDistance + Round(problem.Matrix.DistancesMeters[previousMatrixPoint][0]));
            calculatedDuration = checked(calculatedDuration + Round(problem.Matrix.DurationsSeconds[previousMatrixPoint][0]));
            if (calculatedLoad != vehicle.LoadGrams)
                throw new InvalidOperationException("A carga do veículo diverge da soma de suas paradas.");
            calculatedDuration = checked(calculatedDuration + vehicle.Stops.Count * solution.ServiceDurationSeconds);
            if (calculatedDistance != vehicle.DistanceMeters || calculatedDuration != vehicle.DurationSeconds)
                throw new InvalidOperationException("As métricas do veículo divergem da sequência de paradas.");
            totalLoad = checked(totalLoad + vehicle.LoadGrams);
            totalDistance = checked(totalDistance + vehicle.DistanceMeters);
            totalDuration = checked(totalDuration + vehicle.DurationSeconds);
        }

        if (visitedBlocks.Any(count => count != 1))
            throw new InvalidOperationException("Cada parada de cliente deve aparecer exatamente uma vez na solução.");
        if (totalLoad != problem.Blocks.Sum(block => block.WeightGrams))
            throw new InvalidOperationException("A carga total não foi preservada pela solução.");
        if (totalDistance != solution.TotalDistanceMeters || totalDuration != solution.TotalDurationSeconds)
            throw new InvalidOperationException("As métricas totais divergem da soma dos veículos.");
    }

    private static long Round(decimal value) => Decimal.ToInt64(decimal.Round(
        value, 0, MidpointRounding.AwayFromZero));
}
