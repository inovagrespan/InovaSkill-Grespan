using System.Text.Json.Serialization;

namespace InovaSkill.Importer.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RouteOptimizationScope
{
    SingleRoute,
    AllRoutes
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RouteOptimizationRequestedFrom
{
    RouteScreen,
    GlobalOptimizationScreen,
    Chat,
    ScheduledJob,
    InternalProcess
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RouteOptimizationStatus
{
    Pending,
    LoadingData,
    BuildingProblem,
    CalculatingDistanceMatrix,
    SearchingSolutions,
    ComparingScenarios,
    PersistingResult,
    Completed,
    NoChangeRecommended,
    InsufficientData,
    NoFeasibleSolution,
    Cancelled,
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RouteOptimizationActionType
{
    BuildBalancedRoutePlan,
    OptimizeStopSequence,
    ReallocateCities,
    ChangeTruck,
    NoChange,
    NoFeasibleSolution
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RouteOptimizationConfidence
{
    High,
    Medium,
    Insufficient
}
