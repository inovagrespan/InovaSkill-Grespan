using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Application.RouteImports;

public static class RouteOptimizationCodes
{
    public const string JobType = "PROCESS_ROUTE_OPTIMIZATION";
    public const string AlgorithmVersion = "global-route-v5-stop-sequence";
    public const string RulesVersion = "occupancy-and-road-sequence-v1";
}

public sealed record RouteOptimizationStartRequest(
    RouteOptimizationScope Scope,
    DateOnly ReferenceDate,
    Guid? TargetRouteId,
    RouteOptimizationRequestedFrom RequestedFrom,
    long RequestedByUserId,
    Guid? SnapshotImportId = null);

public sealed record RouteOptimizationRunDto(
    Guid Id,
    RouteOptimizationScope Scope,
    Guid? TargetRouteId,
    DateOnly ReferenceDate,
    RouteOptimizationRequestedFrom RequestedFrom,
    RouteOptimizationStatus Status,
    RouteOptimizationStatus ProgressStage,
    decimal? ProgressPercentage,
    string AlgorithmVersion,
    string RulesVersion,
    string? InputHash,
    RouteOptimizationConfidence Confidence,
    Guid? SnapshotImportId,
    long? SnapshotImportVersion,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<RouteOptimizationScenarioDto> Scenarios);

public sealed record RouteOptimizationScenarioDto(
    Guid Id,
    int Rank,
    decimal Score,
    RouteOptimizationActionType ActionType,
    bool IsRecommended,
    RouteOptimizationConfidence Confidence,
    decimal? EstimatedDistanceChangeKm,
    RouteOptimizationMetricsDto CurrentMetrics,
    RouteOptimizationMetricsDto ProposedMetrics,
    IReadOnlyList<RouteOptimizationReasonDto> Reasons,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<RouteCityReallocationDto> CityReallocations,
    RouteTruckChangeDto? TruckChange,
    IReadOnlyList<RouteSequenceOptimizationDto>? RouteSequences = null);

public sealed record RouteOptimizationMetricsDto(
    Guid RouteId,
    string RouteName,
    string VehicleTypeName,
    decimal? CapacityKg,
    decimal LoadKg,
    decimal? Occupancy,
    string OccupancyLevel,
    IReadOnlyList<string> Cities);

public sealed record RouteOptimizationReasonDto(string Code, string Message);

public sealed record RouteCityReallocationDto(
    Guid CityId,
    string CityName,
    Guid SourceRouteId,
    string SourceRouteName,
    Guid DestinationRouteId,
    string DestinationRouteName,
    decimal CityLoadKg,
    decimal? SourceOccupancyBefore,
    decimal? SourceOccupancyAfter,
    decimal? DestinationOccupancyBefore,
    decimal? DestinationOccupancyAfter,
    decimal EstimatedDistanceChangeKm,
    IReadOnlyList<RouteOptimizationReasonDto> Reasons);

public sealed record RouteTruckChangeDto(
    Guid CurrentTruckModelId,
    string CurrentTruckModelName,
    decimal? CurrentCapacityKg,
    Guid ProposedTruckModelId,
    string ProposedTruckModelName,
    decimal ProposedCapacityKg,
    decimal? OccupancyBefore,
    decimal OccupancyAfter,
    IReadOnlyList<RouteOptimizationReasonDto> Reasons);

public sealed record RouteSequenceOptimizationDto(
    Guid RouteId,
    string RouteName,
    IReadOnlyList<RouteSequenceStopDto> CurrentStops,
    IReadOnlyList<RouteSequenceStopDto> ProposedStops,
    decimal CurrentDistanceKm,
    decimal ProposedDistanceKm,
    decimal DistanceReductionKm,
    decimal DistanceReductionPercentage,
    int CurrentDurationMinutes,
    int ProposedDurationMinutes,
    int DurationReductionMinutes,
    string MatrixMethod);

public sealed record RouteSequenceStopDto(
    Guid CityId,
    string CityName,
    int Sequence,
    decimal LoadKg);

public interface IRouteOptimizationService
{
    Task<RouteOptimizationRunDto> StartOptimizationAsync(
        RouteOptimizationStartRequest request,
        CancellationToken cancellationToken);

    Task<RouteOptimizationRunDto?> GetOptimizationResultAsync(
        Guid optimizationRunId,
        CancellationToken cancellationToken);

    Task<RouteOptimizationRunDto?> GetLatestGlobalOptimizationAsync(
        DateOnly? referenceDate,
        CancellationToken cancellationToken);

    Task<RouteLatestOptimizationDto> GetLatestRouteOptimizationAsync(
        Guid routeId,
        DateOnly? referenceDate,
        CancellationToken cancellationToken);
}

public interface IRouteOptimizationProcessingService
{
    Task ProcessAsync(Guid optimizationRunId, CancellationToken cancellationToken);
}

public interface IRouteOptimizationJobDispatcher
{
    string Enqueue(Guid optimizationRunId);
}

public interface IRouteOptimizationSolver
{
    bool CanHandle(RouteOptimizationScope scope);

    Task<RouteOptimizationSolution> SolveAsync(
        RouteOptimizationProblem problem,
        CancellationToken cancellationToken);
}

public interface IDistanceMatrixProvider
{
    string Method { get; }

    Task<decimal> GetDistanceKmAsync(
        GeoPoint origin,
        GeoPoint destination,
        CancellationToken cancellationToken);
}

public interface IRouteTravelMatrixProvider
{
    string Method { get; }

    Task<RouteTravelMatrix> GetMatrixAsync(
        IReadOnlyList<GeoPoint> points,
        CancellationToken cancellationToken);
}

public interface IRouteStopSequenceOptimizer
{
    Task<RouteStopSequenceResult> OptimizeAsync(
        IReadOnlyList<OptimizationCity> stops,
        CancellationToken cancellationToken);
}

public sealed record RouteTravelMatrix(
    IReadOnlyList<IReadOnlyList<decimal>> DistancesKm,
    IReadOnlyList<IReadOnlyList<int>> DurationsMinutes,
    string Method);

public sealed record RouteStopSequenceResult(
    IReadOnlyList<OptimizationCity> Stops,
    decimal CurrentDistanceKm,
    decimal ProposedDistanceKm,
    int CurrentDurationMinutes,
    int ProposedDurationMinutes,
    string MatrixMethod);

public sealed record GeoPoint(decimal Latitude, decimal Longitude);

public sealed record RouteOptimizationProblem(
    RouteOptimizationScope Scope,
    DateOnly ReferenceDate,
    Guid? TargetRouteId,
    Guid SnapshotImportId,
    long SnapshotImportVersion,
    IReadOnlyList<OptimizationRoute> Routes,
    IReadOnlyList<OptimizationTruckModel> TruckModels,
    OptimizationConstraints Constraints,
    string InputHash);

public sealed record OptimizationRoute(
    Guid RouteId,
    string Name,
    string Weekday,
    Guid TruckModelId,
    string TruckModelName,
    decimal? CapacityKg,
    decimal LoadKg,
    decimal? Occupancy,
    IReadOnlyList<OptimizationCity> Cities);

public sealed record OptimizationCity(
    Guid CityId,
    string Name,
    decimal LoadKg,
    GeoPoint? Location,
    int Sequence);

public sealed record OptimizationTruckModel(
    Guid TruckModelId,
    string Name,
    decimal CapacityKg);

public sealed record OptimizationConstraints(
    int MaximumMovedCities,
    int MaximumCandidateRoutes,
    decimal MinimumOccupancyImprovement,
    decimal MaximumDestinationOccupancy,
    decimal MaximumEstimatedInsertionDistanceKm);

public sealed record RouteOptimizationSolution(
    RouteOptimizationStatus Status,
    RouteOptimizationConfidence Confidence,
    IReadOnlyList<RouteOptimizationScenarioCandidate> Scenarios,
    IReadOnlyList<RouteOptimizationReasonDto> Reasons,
    IReadOnlyList<string> Warnings);

public sealed record RouteOptimizationScenarioCandidate(
    decimal Score,
    RouteOptimizationActionType ActionType,
    RouteOptimizationConfidence Confidence,
    decimal? EstimatedDistanceChangeKm,
    RouteOptimizationMetricsDto CurrentMetrics,
    RouteOptimizationMetricsDto ProposedMetrics,
    IReadOnlyList<RouteOptimizationReasonDto> Reasons,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<RouteCityReallocationDto> CityReallocations,
    RouteTruckChangeDto? TruckChange,
    IReadOnlyList<RouteSequenceOptimizationDto>? RouteSequences = null);

public sealed record RouteLatestOptimizationDto(
    RouteOptimizationStatus Status,
    Guid? RunId,
    DateTime? CalculatedAt,
    Guid? SnapshotImportId,
    long? SourceVersion,
    bool IsStale,
    RouteOptimizationRouteProjectionDto? Route,
    string Message);

public sealed record RouteOptimizationRouteProjectionDto(
    Guid RouteId,
    string RouteName,
    decimal? CurrentOccupancy,
    decimal? ProposedOccupancy,
    decimal? CurrentCapacityKg,
    decimal? ProposedCapacityKg,
    IReadOnlyList<RouteOptimizationCityProjectionDto> AddedCities,
    IReadOnlyList<RouteOptimizationCityProjectionDto> RemovedCities,
    IReadOnlyList<RouteOptimizationReasonDto> Reasons,
    IReadOnlyList<string> Warnings);

public sealed record RouteOptimizationCityProjectionDto(
    Guid CityId,
    string CityName,
    Guid RelatedRouteId,
    string RelatedRouteName,
    decimal CityLoadKg);
