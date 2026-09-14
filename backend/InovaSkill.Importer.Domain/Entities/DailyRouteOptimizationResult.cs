namespace InovaSkill.Importer.Domain.Entities;

public sealed class DailyRouteOptimizationResult
{
    public Guid Id { get; set; }
    public Guid RouteImportId { get; set; }
    public RouteImport? RouteImport { get; set; }
    public Guid JobExecutionId { get; set; }
    public JobExecution? JobExecution { get; set; }
    public Guid? InheritedFromResultId { get; set; }
    public DailyRouteOptimizationResult? InheritedFromResult { get; set; }
    public string Weekday { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public decimal CurrentDistanceMeters { get; set; }
    public decimal CurrentDurationSeconds { get; set; }
    public decimal ProposedDistanceMeters { get; set; }
    public decimal ProposedDurationSeconds { get; set; }
    public int CurrentVehicleCount { get; set; }
    public int ProposedVehicleCount { get; set; }
    public int AdditionalVehicleCount { get; set; }
    public decimal AdditionalCapacityKg { get; set; }
    public decimal TotalWeightKg { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<DailyRouteOptimizationVehicle> Vehicles { get; set; } = [];
    public ICollection<DailyRouteOptimizationIssue> Issues { get; set; } = [];
}

public sealed class DailyRouteOptimizationIssue
{
    public Guid Id { get; set; }
    public Guid ResultId { get; set; }
    public DailyRouteOptimizationResult? Result { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? RouteId { get; set; }
    public Route? Route { get; set; }
    public Guid? RouteEntryId { get; set; }
    public RouteEntry? RouteEntry { get; set; }
    public Guid? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public Guid? VehicleTypeId { get; set; }
    public VehicleType? VehicleType { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CurrentValue { get; set; }
    public bool CanResolve { get; set; }
}

public sealed class DailyRouteOptimizationVehicle
{
    public Guid Id { get; set; }
    public Guid ResultId { get; set; }
    public DailyRouteOptimizationResult? Result { get; set; }
    public Guid VehicleTypeId { get; set; }
    public VehicleType? VehicleType { get; set; }
    public Guid? SourceRouteId { get; set; }
    public Route? SourceRoute { get; set; }
    public int Sequence { get; set; }
    public bool IsAdditional { get; set; }
    public bool IsIdle { get; set; }
    public decimal CapacityKg { get; set; }
    public decimal LoadKg { get; set; }
    public decimal Occupancy { get; set; }
    public decimal DistanceMeters { get; set; }
    public decimal DurationSeconds { get; set; }
    public ICollection<DailyRouteOptimizationStop> Stops { get; set; } = [];
}

public sealed class DailyRouteOptimizationStop
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public DailyRouteOptimizationVehicle? Vehicle { get; set; }
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public int Sequence { get; set; }
    public decimal WeightKg { get; set; }
    public decimal DistanceFromPreviousMeters { get; set; }
    public decimal DurationFromPreviousSeconds { get; set; }
}
