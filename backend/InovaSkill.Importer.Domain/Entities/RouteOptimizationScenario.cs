using InovaSkill.Importer.Domain.Enums;

namespace InovaSkill.Importer.Domain.Entities;

public sealed class RouteOptimizationScenario
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public RouteOptimizationRun? Run { get; set; }
    public int Rank { get; set; }
    public decimal Score { get; set; }
    public RouteOptimizationActionType ActionType { get; set; }
    public bool IsRecommended { get; set; }
    public RouteOptimizationConfidence Confidence { get; set; }
    public decimal? EstimatedDistanceChangeKm { get; set; }
    public string CurrentMetricsJson { get; set; } = "{}";
    public string ProposedMetricsJson { get; set; } = "{}";
    public string WarningsJson { get; set; } = "[]";
    public string ReasonsJson { get; set; } = "[]";
    public string CityReallocationsJson { get; set; } = "[]";
    public string? TruckChangeJson { get; set; }
    public string RouteSequencesJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
}
