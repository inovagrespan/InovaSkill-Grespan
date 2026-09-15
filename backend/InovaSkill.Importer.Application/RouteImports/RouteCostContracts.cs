namespace InovaSkill.Importer.Application.RouteImports;

public static class RouteCostPolicy
{
    public const int MinimumAxleCount = 2;
    public const int MaximumAxleCount = 9;
    public const int FuelLiterDecimals = 3;
    public const int CurrencyDecimals = 2;
    public const int AcceloAxleCount = 2;
    public const decimal AcceloMinimumEfficiencyKmPerLiter = 5.5m;
    public const decimal AcceloMaximumEfficiencyKmPerLiter = 7m;
    public const int TocoAxleCount = 2;
    public const decimal TocoMinimumEfficiencyKmPerLiter = 3.8m;
    public const decimal TocoMaximumEfficiencyKmPerLiter = 4.5m;
    public const int TruckAxleCount = 3;
    public const decimal TruckMinimumEfficiencyKmPerLiter = 3.2m;
    public const decimal TruckMaximumEfficiencyKmPerLiter = 4m;

    public static VehicleCostConfiguration? FindKnownVehicleConfiguration(string vehicleTypeName)
    {
        var normalized = MunicipalityNameNormalizer.Normalize(vehicleTypeName);
        if (normalized.Contains("ACCELO", StringComparison.Ordinal) ||
            normalized.Contains("ACELO", StringComparison.Ordinal))
            return new(AcceloAxleCount, AcceloMinimumEfficiencyKmPerLiter, AcceloMaximumEfficiencyKmPerLiter);
        if (normalized.Contains("TOCO", StringComparison.Ordinal))
            return new(TocoAxleCount, TocoMinimumEfficiencyKmPerLiter, TocoMaximumEfficiencyKmPerLiter);
        if (normalized.Contains("TRUCK", StringComparison.Ordinal))
            return new(TruckAxleCount, TruckMinimumEfficiencyKmPerLiter, TruckMaximumEfficiencyKmPerLiter);
        return null;
    }

    public static RouteFuelCost CalculateFuel(
        decimal distanceMeters,
        decimal minimumEfficiencyKmPerLiter,
        decimal maximumEfficiencyKmPerLiter,
        decimal dieselPricePerLiter)
    {
        if (distanceMeters < 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        ValidateVehicleConfiguration(MinimumAxleCount, minimumEfficiencyKmPerLiter, maximumEfficiencyKmPerLiter);
        if (dieselPricePerLiter <= 0) throw new ArgumentOutOfRangeException(nameof(dieselPricePerLiter));

        var kilometers = distanceMeters / 1000m;
        var minimumLiters = decimal.Round(kilometers / maximumEfficiencyKmPerLiter, FuelLiterDecimals, MidpointRounding.AwayFromZero);
        var maximumLiters = decimal.Round(kilometers / minimumEfficiencyKmPerLiter, FuelLiterDecimals, MidpointRounding.AwayFromZero);
        return new RouteFuelCost(
            minimumLiters,
            maximumLiters,
            RoundCurrency(minimumLiters * dieselPricePerLiter),
            RoundCurrency(maximumLiters * dieselPricePerLiter));
    }

    public static void ValidateVehicleConfiguration(
        int axleCount,
        decimal minimumEfficiencyKmPerLiter,
        decimal maximumEfficiencyKmPerLiter)
    {
        if (axleCount is < MinimumAxleCount or > MaximumAxleCount)
            throw new ArgumentOutOfRangeException(nameof(axleCount), "A quantidade de eixos deve estar entre 2 e 9.");
        if (minimumEfficiencyKmPerLiter <= 0 || maximumEfficiencyKmPerLiter <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumEfficiencyKmPerLiter), "As eficiências devem ser positivas.");
        if (minimumEfficiencyKmPerLiter > maximumEfficiencyKmPerLiter)
            throw new ArgumentException("A eficiência mínima não pode ser maior que a máxima.");
    }

    public static decimal RoundCurrency(decimal value) =>
        decimal.Round(value, CurrencyDecimals, MidpointRounding.AwayFromZero);
}

public sealed record VehicleCostConfiguration(
    int AxleCount,
    decimal MinimumFuelEfficiencyKmPerLiter,
    decimal MaximumFuelEfficiencyKmPerLiter);

public sealed record RouteFuelCost(
    decimal MinimumLiters,
    decimal MaximumLiters,
    decimal MinimumCost,
    decimal MaximumCost);

public sealed record TollPlazaDefinition(
    string Code,
    string Name,
    string OperatorName,
    string Highway,
    decimal Kilometer,
    string Municipality,
    decimal Latitude,
    decimal Longitude,
    decimal AutomaticDiscountRate,
    IReadOnlyDictionary<int, decimal> CommercialManualTariffsByAxle);

public sealed record TollCatalogDefinition(
    string Version,
    DateOnly? EffectiveFrom,
    IReadOnlyList<TollPlazaDefinition> Plazas);

public sealed record TollPassageEstimate(
    TollPlazaDefinition Plaza,
    int AxleCount,
    int Passages,
    decimal AutomaticUnitTariff,
    decimal TotalCost);

public sealed record RouteTollEstimate(
    IReadOnlyList<TollPassageEstimate> Passages,
    int TotalPassages,
    decimal TotalCost);

public interface ITollCatalog
{
    TollCatalogDefinition Current { get; }
    RouteTollEstimate Estimate(IReadOnlyList<IReadOnlyList<decimal>> geometry, int axleCount);
}
