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
    public const int OccupancyScale = 100;
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

public sealed record RouteOptimizationBlock(Guid MunicipalityId, string Name, long WeightGrams);
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
    long TotalDurationSeconds);

public interface IDailyRouteOptimizationSolver
{
    RouteOptimizationSolution Solve(RouteOptimizationProblem problem);
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
        var matrixPointByMunicipality = problem.Matrix.Points
            .Select((point, index) => (point, index))
            .Where(item => item.point.Type == OsrmMatrixPointTypes.Municipality)
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
                var nextMatrixPoint = matrixPointByMunicipality[problem.Blocks[stop.BlockIndex].MunicipalityId];
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
            if (calculatedDistance != vehicle.DistanceMeters || calculatedDuration != vehicle.DurationSeconds)
                throw new InvalidOperationException("As métricas do veículo divergem da sequência de paradas.");
            totalLoad = checked(totalLoad + vehicle.LoadGrams);
            totalDistance = checked(totalDistance + vehicle.DistanceMeters);
            totalDuration = checked(totalDuration + vehicle.DurationSeconds);
        }

        if (visitedBlocks.Any(count => count != 1))
            throw new InvalidOperationException("Cada bloco municipal deve aparecer exatamente uma vez na solução.");
        if (totalLoad != problem.Blocks.Sum(block => block.WeightGrams))
            throw new InvalidOperationException("A carga total não foi preservada pela solução.");
        if (totalDistance != solution.TotalDistanceMeters || totalDuration != solution.TotalDurationSeconds)
            throw new InvalidOperationException("As métricas totais divergem da soma dos veículos.");
    }

    private static long Round(decimal value) => Decimal.ToInt64(decimal.Round(
        value, 0, MidpointRounding.AwayFromZero));
}
