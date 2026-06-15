using InovaSkill.Importer.Application.Analytics;

namespace InovaSkill.Importer.Tests.Analytics;

public sealed class CustomerInsightsCalculatorTests
{
    [Fact]
    public void Build_CalculatesAllInsightMetricsFromConsistentHistory()
    {
        var purchaseDays = new[]
        {
            new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 21, 10, 0, 0, DateTimeKind.Utc)
        };

        var monthlyRows = new[]
        {
            new CustomerMonthlyMetric(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 90m, 9m),
            new CustomerMonthlyMetric(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), 120m, 12m),
            new CustomerMonthlyMetric(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), 150m, 15m),
            new CustomerMonthlyMetric(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), 180m, 18m)
        };

        var result = CustomerInsightsCalculator.Build(
            purchaseDays,
            monthlyRows,
            movingAverageWindowMonths: 3,
            todayUtc: new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(10m, result.AveragePurchaseFrequencyDays);
        Assert.Equal(new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc), result.EstimatedNextPurchaseDate);
        Assert.Equal(150m, result.PredictedRevenue);
        Assert.Equal(15m, result.PredictedQuantity);
        Assert.Equal("Estabilidade", result.ConsumptionTrend);
        Assert.Equal("Sem risco", result.RiskLevel);
        Assert.Equal(10, result.DaysWithoutPurchase);
        Assert.Equal(1m, result.RiskScore);
        Assert.Null(result.FrequencyReason);
        Assert.Null(result.NextPurchaseReason);
        Assert.Null(result.RevenuePredictionReason);
        Assert.Null(result.QuantityPredictionReason);
        Assert.Null(result.RiskReason);
        Assert.Equal(4, result.MonthlyHistoryPeriods);
    }

    [Fact]
    public void Build_ReturnsExplanatoryReasonsWhenHistoryIsInsufficient()
    {
        var purchaseDays = new[]
        {
            new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc)
        };

        var monthlyRows = new[]
        {
            new CustomerMonthlyMetric(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), 50m, 5m),
            new CustomerMonthlyMetric(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), 60m, 6m)
        };

        var result = CustomerInsightsCalculator.Build(
            purchaseDays,
            monthlyRows,
            movingAverageWindowMonths: 3,
            todayUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Null(result.AveragePurchaseFrequencyDays);
        Assert.Null(result.EstimatedNextPurchaseDate);
        Assert.Null(result.PredictedRevenue);
        Assert.Null(result.PredictedQuantity);
        Assert.Equal("Estabilidade", result.ConsumptionTrend);
        Assert.Equal(RiskClassifier.InsufficientHistory, result.RiskLevel);
        Assert.Equal(12, result.DaysWithoutPurchase);
        Assert.Null(result.RiskScore);
        Assert.Equal("Necessário pelo menos 2 compras em datas diferentes para calcular frequência.", result.FrequencyReason);
        Assert.Equal("Necessário calcular a frequência média antes de estimar a próxima compra.", result.NextPurchaseReason);
        Assert.Equal("Necessário histórico de pelo menos 3 meses para previsão mensal por média móvel.", result.RevenuePredictionReason);
        Assert.Equal("Necessário histórico de pelo menos 3 meses para previsão mensal por média móvel.", result.QuantityPredictionReason);
        Assert.Equal("Necessário frequência média e data da última compra para calcular risco.", result.RiskReason);
        Assert.Equal(2, result.MonthlyHistoryPeriods);
    }

    [Fact]
    public void Build_UsesCustomMinimumPeriodsForPredictions()
    {
        var purchaseDays = new[]
        {
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc)
        };

        var monthlyRows = new[]
        {
            new CustomerMonthlyMetric(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), 40m, 4m),
            new CustomerMonthlyMetric(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), 80m, 8m)
        };

        var result = CustomerInsightsCalculator.Build(
            purchaseDays,
            monthlyRows,
            movingAverageWindowMonths: 2,
            todayUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            minimumPeriodsForMovingAverage: 2);

        Assert.Equal(60m, result.PredictedRevenue);
        Assert.Equal(6m, result.PredictedQuantity);
        Assert.Null(result.RevenuePredictionReason);
        Assert.Null(result.QuantityPredictionReason);
    }
}
