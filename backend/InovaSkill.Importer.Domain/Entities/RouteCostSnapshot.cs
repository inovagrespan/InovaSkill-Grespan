namespace InovaSkill.Importer.Domain.Entities;

public static class RouteCostScenarios
{
    public const string Actual = "ACTUAL";
    public const string Optimized = "OPTIMIZED";
}

public static class RouteCostPathBases
{
    public const string ExactCustomers = "EXACT_CUSTOMERS";
    public const string OptimizedMunicipalities = "OPTIMIZED_MUNICIPALITIES";
}

public sealed class RouteCostSnapshot
{
    public Guid Id { get; set; }
    public Guid RouteImportId { get; set; }
    public RouteImport? RouteImport { get; set; }
    public Guid JobExecutionId { get; set; }
    public JobExecution? JobExecution { get; set; }
    public string InputFingerprint { get; set; } = string.Empty;
    public decimal? DieselPricePerLiter { get; set; }
    public string TollCatalogVersion { get; set; } = string.Empty;
    public DateOnly? TollEffectiveFrom { get; set; }
    public DateTime CalculatedAt { get; set; }
    public ICollection<RouteCostItem> Items { get; set; } = [];
}

public sealed class RouteCostItem
{
    public Guid Id { get; set; }
    public Guid SnapshotId { get; set; }
    public RouteCostSnapshot? Snapshot { get; set; }
    public string Scenario { get; set; } = string.Empty;
    public string Weekday { get; set; } = string.Empty;
    public Guid? RouteId { get; set; }
    public Route? Route { get; set; }
    public Guid? OptimizationResultId { get; set; }
    public DailyRouteOptimizationResult? OptimizationResult { get; set; }
    public Guid? OptimizationVehicleId { get; set; }
    public DailyRouteOptimizationVehicle? OptimizationVehicle { get; set; }
    public Guid? VehicleTypeId { get; set; }
    public VehicleType? VehicleType { get; set; }
    public string Label { get; set; } = string.Empty;
    public string PathBasis { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? UnavailableReason { get; set; }
    public decimal? DistanceMeters { get; set; }
    public decimal? DurationSeconds { get; set; }
    public decimal? MinimumFuelLiters { get; set; }
    public decimal? MaximumFuelLiters { get; set; }
    public decimal? MinimumFuelCost { get; set; }
    public decimal? MaximumFuelCost { get; set; }
    public decimal TollCost { get; set; }
    public int TollPassages { get; set; }
    public decimal? MinimumTotalCost { get; set; }
    public decimal? MaximumTotalCost { get; set; }
    public ICollection<RouteCostTollPassage> TollPassageItems { get; set; } = [];
}

public sealed class RouteCostTollPassage
{
    public Guid Id { get; set; }
    public Guid RouteCostItemId { get; set; }
    public RouteCostItem? RouteCostItem { get; set; }
    public string TollPlazaCode { get; set; } = string.Empty;
    public string TollPlazaName { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string Highway { get; set; } = string.Empty;
    public decimal Kilometer { get; set; }
    public int AxleCount { get; set; }
    public int Passages { get; set; }
    public decimal AutomaticUnitTariff { get; set; }
    public decimal TotalCost { get; set; }
}
