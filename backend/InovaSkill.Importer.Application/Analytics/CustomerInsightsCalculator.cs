namespace InovaSkill.Importer.Application.Analytics;

public static class CustomerInsightsCalculator
{
    private const int DefaultMinimumPeriodsForMovingAverage = 3;
    private const int MinimumPurchaseDaysForFrequency = 2;

    public static CustomerInsightsMetrics Build(
        IReadOnlyList<DateTime> purchaseDays,
        IReadOnlyList<CustomerMonthlyMetric> monthlyRows,
        int movingAverageWindowMonths,
        DateTime todayUtc,
        int minimumPeriodsForMovingAverage = DefaultMinimumPeriodsForMovingAverage)
    {
        var orderedPurchaseDays = purchaseDays.Select(x => x.Date).Distinct().OrderBy(x => x).ToList();
        DateTime? lastPurchaseDate = orderedPurchaseDays.Count == 0 ? null : orderedPurchaseDays[^1];
        var averageFrequencyDays = AverageDaysBetweenPurchases(orderedPurchaseDays);
        DateTime? estimatedNextPurchaseDate = averageFrequencyDays.HasValue && lastPurchaseDate.HasValue
            ? lastPurchaseDate.Value.AddDays((double)averageFrequencyDays.Value)
            : null;

        var orderedMonthlyRows = monthlyRows.OrderBy(x => x.MonthStart).ToList();
        var revenueSeries = orderedMonthlyRows.Select(x => x.Revenue).ToList();
        var quantitySeries = orderedMonthlyRows.Select(x => x.Quantity).ToList();
        var predictedRevenue = MovingAverageCalculator.Calculate(revenueSeries, movingAverageWindowMonths, minimumPeriodsForMovingAverage);
        var predictedQuantity = MovingAverageCalculator.Calculate(quantitySeries, movingAverageWindowMonths, minimumPeriodsForMovingAverage);
        var daysWithoutPurchase = lastPurchaseDate.HasValue
            ? Math.Max(0, (int)(todayUtc.Date - lastPurchaseDate.Value.Date).TotalDays)
            : 0;
        var riskScore = averageFrequencyDays is > 0 ? daysWithoutPurchase / averageFrequencyDays : null;

        return new CustomerInsightsMetrics(
            averageFrequencyDays,
            estimatedNextPurchaseDate,
            predictedRevenue,
            predictedQuantity,
            ConsumptionTrendClassifier.Classify(quantitySeries),
            RiskClassifier.Classify(riskScore),
            daysWithoutPurchase,
            riskScore,
            averageFrequencyDays.HasValue ? null : "Necessário pelo menos 2 compras em datas diferentes para calcular frequência.",
            estimatedNextPurchaseDate.HasValue ? null : "Necessário calcular a frequência média antes de estimar a próxima compra.",
            predictedRevenue.HasValue ? null : $"Necessário histórico de pelo menos {minimumPeriodsForMovingAverage} meses para previsão mensal por média móvel.",
            predictedQuantity.HasValue ? null : $"Necessário histórico de pelo menos {minimumPeriodsForMovingAverage} meses para previsão mensal por média móvel.",
            riskScore.HasValue ? null : "Necessário frequência média e data da última compra para calcular risco.",
            orderedMonthlyRows.Count);
    }

    private static decimal? AverageDaysBetweenPurchases(IReadOnlyList<DateTime> purchaseDays)
    {
        if (purchaseDays.Count < MinimumPurchaseDaysForFrequency)
        {
            return null;
        }

        var deltas = purchaseDays.Zip(purchaseDays.Skip(1), (a, b) => (decimal)(b - a).TotalDays).ToList();
        return deltas.Count == 0 ? null : deltas.Average();
    }
}
