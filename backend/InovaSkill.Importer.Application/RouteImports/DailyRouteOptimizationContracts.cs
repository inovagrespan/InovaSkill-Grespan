namespace InovaSkill.Importer.Application.RouteImports;

public static class DailyRouteOptimizationPolicy
{
    public const string JobType = "DAILY_ROUTE_OPTIMIZATION";
    public const int ContractVersion = 1;
    public const int WeightScale = 100;
    public const int SolverTimeLimitSeconds = 5;
    public const int MonetaryCostScale = 100;
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
    public const string RulesVersion = "daily-v7-persisted-fuel-price";
}

public static class DailyRouteOptimizationStatuses
{
    public const string Optimized = "Optimized";
    public const string NoImprovement = "NoImprovement";
    public const string Infeasible = "Infeasible";
    public const string InsufficientData = "InsufficientData";
}

public sealed record DailyOptimizationVehicle(
    Guid? RouteId,
    Guid VehicleTypeId,
    string RouteName,
    string VehicleType,
    decimal CapacityKg,
    bool IsRental);

public sealed record DailyOptimizationStop(
    Guid MunicipalityId,
    string MunicipalityName,
    decimal LoadKg,
    int Deliveries,
    Guid OriginalRouteId,
    int OriginalSequence);

public sealed record DailyOptimizationRoute(
    int Sequence,
    Guid? OriginalRouteId,
    string Name,
    string VehicleType,
    bool IsRental,
    decimal LoadKg,
    decimal CapacityKg,
    decimal Occupancy,
    decimal DistanceMeters,
    decimal DurationSeconds,
    decimal EstimatedFuelLiters,
    decimal TankCapacityLiters,
    decimal TankUsagePercent,
    decimal RemainingAutonomyKm,
    decimal? RentalDailyCostMinimum,
    decimal? RentalDailyCostMaximum,
    IReadOnlyList<DailyOptimizationStop> Stops);

public sealed record DailyOptimizationMetrics(
    decimal DistanceMeters,
    decimal DurationSeconds,
    int VehiclesUsed,
    int RentalVehicles,
    decimal MaximumOccupancy);

public sealed record DailyRouteOptimizationResult(
    Guid ImportId,
    string Weekday,
    string Status,
    string RulesVersion,
    string MatrixSource,
    string? Message,
    DailyOptimizationMetrics Current,
    DailyOptimizationMetrics Proposed,
    IReadOnlyList<DailyOptimizationRoute> Routes);

public sealed record DailyRouteOptimizationProblem(
    Guid ImportId,
    string Weekday,
    decimal DieselPricePerLiter,
    IReadOnlyList<OsrmMatrixPoint> Points,
    IReadOnlyList<IReadOnlyList<decimal>> DurationsSeconds,
    IReadOnlyList<IReadOnlyList<decimal>> DistancesMeters,
    IReadOnlyList<DailyOptimizationStop> Stops,
    IReadOnlyList<DailyOptimizationVehicle> OwnVehicles,
    IReadOnlyList<DailyOptimizationVehicle> RentalVehicleTypes);

public interface IDailyRouteOptimizer
{
    DailyRouteOptimizationResult Optimize(DailyRouteOptimizationProblem problem, string matrixSource);
}
