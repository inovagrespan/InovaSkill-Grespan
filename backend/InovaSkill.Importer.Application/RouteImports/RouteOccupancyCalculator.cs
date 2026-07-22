namespace InovaSkill.Importer.Application.RouteImports;

public sealed record RouteOccupancyInput(
    decimal? TotalWeightKg,
    decimal? WeightCapacityKg,
    decimal? TotalVolumeM3,
    decimal? VolumeCapacityM3,
    int? TotalPallets,
    int? PalletCapacity);

public sealed record RouteOccupancyResult(
    decimal? WeightOccupancy,
    decimal? VolumeOccupancy,
    decimal? PalletOccupancy,
    decimal? OverallOccupancy)
{
    public bool HasAvailableCapacity => OverallOccupancy.HasValue;
}

public static class RouteOccupancyCalculator
{
    private const decimal MaximumOccupancy = 1m;

    public static RouteOccupancyResult Calculate(RouteOccupancyInput input)
    {
        var weight = DivideWhenCapacityIsAvailable(input.TotalWeightKg, input.WeightCapacityKg);
        var volume = DivideWhenCapacityIsAvailable(input.TotalVolumeM3, input.VolumeCapacityM3);
        var pallets = DivideWhenCapacityIsAvailable(input.TotalPallets, input.PalletCapacity);
        var availableDimensions = new[] { weight, volume, pallets }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return new RouteOccupancyResult(
            weight,
            volume,
            pallets,
            availableDimensions.Length == 0 ? null : availableDimensions.Max());
    }

    private static decimal? DivideWhenCapacityIsAvailable(decimal? total, decimal? capacity) =>
        total.HasValue && capacity is > 0
            ? Math.Min(MaximumOccupancy, Math.Max(0m, total.Value / capacity.Value))
            : null;

    private static decimal? DivideWhenCapacityIsAvailable(int? total, int? capacity) =>
        total.HasValue && capacity is > 0
            ? Math.Min(MaximumOccupancy, Math.Max(0m, (decimal)total.Value / capacity.Value))
            : null;
}
