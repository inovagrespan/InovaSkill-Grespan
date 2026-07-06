namespace InovaSkill.Importer.Application.RouteImports;

public sealed record CustomerMonthlyObservation(
    DateOnly Month,
    decimal SalesWeightKg,
    decimal CalculatedSalesAmount);

public sealed record CustomerProjectionMonth(
    DateOnly Month,
    decimal Forecast,
    decimal LowerBound,
    decimal UpperBound);

public sealed record CustomerProjectionSeries(
    decimal HistoricalMonthlyAverage,
    decimal MonthlyChange,
    decimal? MonthlyChangePercentage,
    decimal RSquared,
    decimal NormalizedRmsePercentage,
    int ActiveMonths,
    string Quality,
    IReadOnlyList<CustomerProjectionMonth> Forecast);

public sealed record CustomerProjectionResult(
    DateOnly BaseStartMonth,
    DateOnly BaseEndMonth,
    CustomerProjectionSeries Weight,
    CustomerProjectionSeries Revenue);

public static class CustomerProjectionCalculator
{
    public const int HistoricalMonthCount = 12;
    public const int ForecastMonthCount = 3;
    public const int MinimumActiveMonths = 4;
    private const double StudentTCritical95PercentForTenDegreesOfFreedom = 2.228;
    private const double NearZero = 1e-9;

    public static CustomerProjectionResult Calculate(IReadOnlyList<CustomerMonthlyObservation> observations)
    {
        if (observations.Count != HistoricalMonthCount)
            throw new ArgumentException($"A projeção exige exatamente {HistoricalMonthCount} meses.", nameof(observations));
        var ordered = observations.OrderBy(item => item.Month).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].Month != ordered[index - 1].Month.AddMonths(1))
                throw new ArgumentException("Os meses da projeção devem ser consecutivos.", nameof(observations));
        }

        return new CustomerProjectionResult(
            ordered[0].Month,
            ordered[^1].Month,
            CalculateSeries(ordered.Select(item => item.SalesWeightKg).ToArray(), ordered[^1].Month, 3),
            CalculateSeries(ordered.Select(item => item.CalculatedSalesAmount).ToArray(), ordered[^1].Month, 2));
    }

    private static CustomerProjectionSeries CalculateSeries(
        IReadOnlyList<decimal> decimalValues,
        DateOnly baseEndMonth,
        int precision)
    {
        var values = decimalValues.Select(decimal.ToDouble).ToArray();
        var count = values.Length;
        var activeMonths = values.Count(value => value > 0);
        var meanX = (count - 1) / 2d;
        var meanY = values.Average();
        var sxx = Enumerable.Range(0, count).Sum(index => Math.Pow(index - meanX, 2));
        var slope = sxx <= NearZero
            ? 0
            : Enumerable.Range(0, count).Sum(index => (index - meanX) * (values[index] - meanY)) / sxx;
        var intercept = meanY - slope * meanX;
        var residuals = Enumerable.Range(0, count)
            .Select(index => values[index] - (intercept + slope * index)).ToArray();
        var sumSquaredErrors = residuals.Sum(value => value * value);
        var totalSumSquares = values.Sum(value => Math.Pow(value - meanY, 2));
        var rSquared = totalSumSquares <= NearZero
            ? (sumSquaredErrors <= NearZero ? 1d : 0d)
            : Math.Clamp(1 - sumSquaredErrors / totalSumSquares, 0, 1);
        var rmse = Math.Sqrt(sumSquaredErrors / count);
        var normalizedRmse = meanY <= NearZero ? 0 : rmse / meanY;
        var quality = ResolveQuality(activeMonths, meanY, rSquared, normalizedRmse);
        var residualStandardError = count > 2 ? Math.Sqrt(sumSquaredErrors / (count - 2)) : 0;
        var forecast = Enumerable.Range(1, ForecastMonthCount).Select(horizon =>
        {
            var x = count - 1 + horizon;
            var predicted = Math.Max(0, intercept + slope * x);
            var predictionStandardError = residualStandardError * Math.Sqrt(
                1 + (1d / count) + Math.Pow(x - meanX, 2) / sxx);
            var margin = StudentTCritical95PercentForTenDegreesOfFreedom * predictionStandardError;
            return new CustomerProjectionMonth(
                baseEndMonth.AddMonths(horizon),
                Round(predicted, precision),
                Round(Math.Max(0, predicted - margin), precision),
                Round(Math.Max(0, predicted + margin), precision));
        }).ToArray();

        return new CustomerProjectionSeries(
            Round(meanY, precision),
            Round(slope, precision),
            meanY <= NearZero ? null : Round(slope / meanY * 100, 1),
            Round(rSquared, 3),
            Round(normalizedRmse * 100, 1),
            activeMonths,
            quality,
            forecast);
    }

    private static string ResolveQuality(int activeMonths, double mean, double rSquared, double normalizedRmse)
    {
        if (activeMonths < MinimumActiveMonths || mean <= NearZero) return "INSUFFICIENT";
        if (activeMonths >= 8 && rSquared >= 0.7 && normalizedRmse <= 0.25) return "HIGH";
        if (activeMonths >= 6 && rSquared >= 0.4 && normalizedRmse <= 0.5) return "MODERATE";
        return "LOW";
    }

    private static decimal Round(double value, int precision) =>
        Math.Round((decimal)value, precision, MidpointRounding.AwayFromZero);
}
