using InovaSkill.Importer.Application.Analytics;

namespace InovaSkill.Importer.Tests.Analytics;

public sealed class CustomerCalculatorsTests
{
    [Fact]
    public void ShouldCalculateAverageTicketUsingRevenueDividedByDistinctOrders()
    {
        // Regra esperada: ticket médio = faturamento total / quantidade de documentos distintos.
        var rows = new List<CustomerMetricTransaction>
        {
            Row("C1", "Cliente A", "NF-1", 100m, 2m, 5m, new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc)),
            Row("C1", "Cliente A", "NF-1", 50m, 1m, 1m, new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc)),
            Row("C1", "Cliente A", "NF-2", 150m, 3m, 4m, new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc))
        };

        var result = CustomerCalculators.BuildCustomerSummary(
            rows,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            currentWeekRevenue: 300m,
            previousWeekRevenue: 250m,
            todayUtc: new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(300m, result.TotalRevenue);
        Assert.Equal(2, result.TotalOrders);
        Assert.Equal(150m, result.AverageTicket);
    }

    [Fact]
    public void ShouldCalculateMonthlyAndWeeklyAveragesUsingOnlyPeriodsWithPurchases()
    {
        // Regra esperada: médias mensal/semanal usam apenas meses/semanas que possuem compras no período filtrado.
        var rows = new List<CustomerMetricTransaction>
        {
            Row("C1", "Cliente A", "NF-1", 100m, 1m, 1m, new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)),
            Row("C1", "Cliente A", "NF-2", 300m, 1m, 1m, new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc)),
            Row("C1", "Cliente A", "NF-3", 200m, 1m, 1m, new DateTime(2026, 5, 19, 0, 0, 0, DateTimeKind.Utc))
        };

        var result = CustomerCalculators.BuildCustomerSummary(
            rows,
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            currentWeekRevenue: 0m,
            previousWeekRevenue: 0m,
            todayUtc: new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(300m, result.AverageRevenueMonthly);
        Assert.Equal(200m, result.AverageRevenueWeekly);
    }

    [Fact]
    public void ShouldReturnNullAveragesWhenFilteredPeriodHasNoTransactions()
    {
        // Regra esperada: cliente existente sem compra no filtro retorna totais zerados e médias nulas.
        var rows = new List<CustomerMetricTransaction>
        {
            Row("C1", "Cliente A", "NF-1", 100m, 1m, 1m, new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc))
        };

        var result = CustomerCalculators.BuildCustomerSummary(
            rows,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            currentWeekRevenue: 0m,
            previousWeekRevenue: 0m,
            todayUtc: new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0m, result.TotalRevenue);
        Assert.Null(result.AverageTicket);
        Assert.Null(result.AverageRevenueMonthly);
        Assert.Null(result.AverageRevenueWeekly);
    }

    [Fact]
    public void ShouldCalculateAveragePurchaseFrequencyFromDistinctPurchaseDays()
    {
        // Regra esperada: frequência média usa intervalos entre dias distintos de compra.
        var rows = new List<CustomerMetricTransaction>
        {
            Row("C1", "Cliente A", "NF-1", 100m, 1m, 1m, new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)),
            Row("C1", "Cliente A", "NF-2", 120m, 1m, 1m, new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc)),
            Row("C1", "Cliente A", "NF-3", 130m, 1m, 1m, new DateTime(2026, 5, 21, 10, 0, 0, DateTimeKind.Utc))
        };

        var result = CustomerCalculators.BuildCustomerSummary(
            rows,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            currentWeekRevenue: 0m,
            previousWeekRevenue: 0m,
            todayUtc: new DateTime(2026, 5, 22, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(10m, result.AverageDaysBetweenPurchases);
    }

    [Fact]
    public void ShouldCalculateSummaryActiveNewAndInactiveCustomersByCustomerKey()
    {
        // Regra esperada: cliente ativo aparece no período atual; novo não aparece no anterior; inativo aparece só no anterior.
        var current = new List<CustomerMetricTransaction>
        {
            Row("C1", "Cliente A", "NF-1", 100m, 1m, 1m, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)),
            Row("C2", "Cliente B", "NF-2", 200m, 1m, 1m, new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc))
        };
        var previousKeys = new[] { "C1|Cliente A", "C3|Cliente C" };

        var result = CustomerCalculators.BuildAnalyticsSummary(current, previousKeys);

        Assert.Equal(2, result.ActiveCustomers);
        Assert.Equal(300m, result.TotalRevenue);
        Assert.Equal(2, result.TotalOrders);
        Assert.Equal(150m, result.AverageTicket);
        Assert.Equal(150m, result.AverageRevenuePerCustomer);
        Assert.Equal(1, result.NewCustomers);
        Assert.Equal(1, result.InactiveCustomers);
    }

    [Fact]
    public void ShouldCalculateRankingVariationAndSortByGrowth()
    {
        // Regra esperada: variação percentual = (atual - anterior) / anterior * 100 e ordenação por maior crescimento.
        var current = new List<CustomerMetricTransaction>
        {
            Row("C1", "Cliente A", "NF-1", 300m, 3m, 6m, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)),
            Row("C2", "Cliente B", "NF-2", 150m, 2m, 4m, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc))
        };
        var previous = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["C1|Cliente A"] = 100m,
            ["C2|Cliente B"] = 100m
        };

        var result = CustomerCalculators.BuildRanking(current, previous, "growth");

        Assert.Equal("C1", result[0].CustomerCode);
        Assert.Equal(200m, result[0].VariationPercent);
        Assert.Equal(50m, result[1].VariationPercent);
    }

    [Fact]
    public void ShouldBuildNewCustomersMonthlyIncludingEmptyMonths()
    {
        // Regra esperada: a série mensal inclui meses sem novos clientes com valor zero.
        var firstPurchases = new[]
        {
            new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = CustomerCalculators.BuildNewCustomersMonthly(
            firstPurchases,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].NewCustomers);
        Assert.Equal(0, result[1].NewCustomers);
        Assert.Equal(1, result[2].NewCustomers);
    }

    [Fact]
    public void ShouldEstimateNextPurchaseAndMovingAveragesFromControlledHistory()
    {
        // Regra esperada: próxima compra = última compra + frequência média; previsão = média móvel dos últimos N meses.
        var purchaseDays = new[]
        {
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc)
        };
        var monthly = new[]
        {
            new CustomerMonthlyMetric(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 90m, 9m),
            new CustomerMonthlyMetric(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), 120m, 12m),
            new CustomerMonthlyMetric(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), 150m, 15m)
        };

        var result = CustomerCalculators.BuildInsights(
            purchaseDays,
            monthly,
            movingAverageWindowMonths: 3,
            todayUtc: new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(10m, result.AveragePurchaseFrequencyDays);
        Assert.Equal(new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc), result.EstimatedNextPurchaseDate);
        Assert.Equal(120m, result.PredictedRevenue);
        Assert.Equal(12m, result.PredictedQuantity);
        Assert.Equal(1m, result.RiskScore);
        Assert.Equal("Sem risco", result.RiskLevel);
    }

    [Theory]
    [InlineData(1.5, "Sem risco")]
    [InlineData(2.0, "Atenção")]
    [InlineData(3.0, "Em risco")]
    [InlineData(3.1, "Crítico")]
    public void ShouldClassifyRiskLevelFromRiskScore(decimal score, string expected)
    {
        // Regra esperada: faixas de risco são inclusivas até 1,5, 2,0 e 3,0.
        Assert.Equal(expected, CustomerCalculators.ResolveRiskLevel(score));
    }

    [Theory]
    [InlineData(new[] { 10, 10, 10, 12, 12, 12 }, "Crescimento")]
    [InlineData(new[] { 10, 10, 10, 9, 9, 9 }, "Queda")]
    [InlineData(new[] { 10, 10, 10, 10, 10, 10 }, "Estabilidade")]
    public void ShouldClassifyConsumptionTrendComparingRecentThreeMonthsWithPreviousThree(int[] quantities, string expected)
    {
        // Regra esperada: tendência compara a média dos 3 meses recentes contra os 3 anteriores com tolerância de 5%.
        var values = quantities.Select(x => (decimal)x).ToList();

        Assert.Equal(expected, CustomerCalculators.ResolveConsumptionTrend(values));
    }

    [Fact]
    public void ShouldCalculateProductShareUsingRevenueParticipation()
    {
        // Regra esperada: participação do produto = receita do produto / receita total do agrupamento * 100.
        var grouped = new List<CustomerProductShareMetrics>
        {
            new("P1", "Produto 1", 2m, 75m, 0m),
            new("P2", "Produto 2", 1m, 25m, 0m)
        };

        var result = CustomerCalculators.BuildProductShares(grouped);

        Assert.Equal(75m, result[0].SharePercent);
        Assert.Equal(25m, result[1].SharePercent);
    }

    [Fact]
    public void ShouldReturnNullVariationWhenPreviousPeriodIsZero()
    {
        // Regra esperada: variação percentual é nula quando a base anterior é zero para evitar divisão inválida.
        Assert.Null(CustomerCalculators.CalculateVariationPercent(100m, 0m));
    }

    private static CustomerMetricTransaction Row(
        string customerCode,
        string customerName,
        string documentNumber,
        decimal totalAmount,
        decimal quantity,
        decimal weight,
        DateTime transactionDate)
    {
        return new CustomerMetricTransaction(
            customerCode,
            customerName,
            "Campinas",
            documentNumber,
            totalAmount,
            quantity,
            weight,
            transactionDate);
    }
}
