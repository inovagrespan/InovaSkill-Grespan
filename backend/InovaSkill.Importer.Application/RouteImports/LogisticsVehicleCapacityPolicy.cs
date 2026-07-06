namespace InovaSkill.Importer.Application.RouteImports;

public static class LogisticsVehicleCapacityPolicy
{
    public const decimal TruckWeightCapacityKg = 10_300m;
    public const decimal TocoWeightCapacityKg = 7_700m;
    public const decimal AceloWeightCapacityKg = 3_300m;

    private static readonly IReadOnlyDictionary<string, decimal> WeightCapacityByVehicleType =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Truck"] = TruckWeightCapacityKg,
            ["Toco"] = TocoWeightCapacityKg,
            ["Acelo"] = AceloWeightCapacityKg
        };

    public static decimal? FindWeightCapacityKg(string vehicleType) =>
        WeightCapacityByVehicleType.TryGetValue(vehicleType, out var capacity)
            ? capacity
            : null;
}
