using InovaSkill.Importer.Application.Analytics;
using System.Globalization;

namespace InovaSkill.Importer.Tests.Analytics;

public sealed class CustomerCommercialHealthAnalyzerTests
{
    private static readonly DateTime Today = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_ClassifiesCriticalCustomer_WhenPurchaseCycleIsOverdue()
    {
        var report = CustomerCommercialHealthAnalyzer.Build(
            [
                BuildTransaction("2026-01-01", "NF-1", "P1", 100m, 10m),
                BuildTransaction("2026-01-08", "NF-2", "P1", 100m, 10m),
                BuildTransaction("2026-01-15", "NF-3", "P1", 100m, 10m)
            ],
            Today);

        Assert.Equal("Crítico", report.Health.Status);
        Assert.True(report.Score.Value < 25);
        Assert.Contains(report.Recommendations, x => x.Title == "Contato imediato");
        Assert.Contains(report.Alerts, x => x.Title == "Cliente sem compra acima da frequência histórica");
    }

    [Fact]
    public void Build_ClassifiesStrongGrowthAndExpansion_WhenRecentRevenueIncreases()
    {
        var report = CustomerCommercialHealthAnalyzer.Build(
            [
                BuildTransaction("2025-12-20", "NF-1", "P1", 100m, 10m),
                BuildTransaction("2026-01-20", "NF-2", "P1", 100m, 10m),
                BuildTransaction("2026-04-15", "NF-3", "P1", 250m, 25m),
                BuildTransaction("2026-05-15", "NF-4", "P2", 250m, 25m)
            ],
            Today);

        Assert.Equal("Forte crescimento", report.Trend.Status);
        Assert.Contains(report.Recommendations, x => x.Title == "Expandir relacionamento");
        Assert.NotNull(report.Potential.ExpectedRevenue);
    }

    [Fact]
    public void Build_DetectsProductDependency_WhenTwoProductsConcentrateRevenue()
    {
        var report = CustomerCommercialHealthAnalyzer.Build(
            [
                BuildTransaction("2026-05-01", "NF-1", "P1", 700m, 70m),
                BuildTransaction("2026-05-08", "NF-2", "P2", 100m, 10m),
                BuildTransaction("2026-05-15", "NF-3", "P3", 100m, 10m),
                BuildTransaction("2026-05-22", "NF-4", "P4", 100m, 10m)
            ],
            Today);

        Assert.Equal("Alta dependência", report.Dependency.Status);
        Assert.Equal(2, report.Dependency.ProductsToReachEightyPercent);
        Assert.Contains(report.Alerts, x => x.Title == "Dependência de produto relevante");
    }

    [Fact]
    public void Build_ReturnsComparisonsForRevenueQuantityOrdersAndAverageTicket()
    {
        var report = CustomerCommercialHealthAnalyzer.Build(
            [
                BuildTransaction("2026-02-15", "NF-1", "P1", 100m, 10m),
                BuildTransaction("2026-05-15", "NF-2", "P1", 200m, 20m),
                BuildTransaction("2026-05-20", "NF-3", "P2", 200m, 20m)
            ],
            Today);

        var comparison = Assert.Single(report.Comparisons, x => x.Label == "Últimos 90 dias");
        Assert.Equal(300m, comparison.RevenueVariationPercent);
        Assert.Equal(300m, comparison.QuantityVariationPercent);
        Assert.Equal(100m, comparison.OrdersVariationPercent);
        Assert.Equal(100m, comparison.AverageTicketVariationPercent);
    }

    private static CustomerCommercialHealthTransaction BuildTransaction(
        string date,
        string document,
        string productCode,
        decimal revenue,
        decimal quantity)
    {
        return new CustomerCommercialHealthTransaction(
            "C1",
            "Cliente A",
            "Campinas",
            "Empresa Alpha",
            document,
            productCode,
            $"Produto {productCode}",
            revenue,
            quantity,
            quantity / 2m,
            DateTime.SpecifyKind(DateTime.Parse(date, CultureInfo.InvariantCulture), DateTimeKind.Utc));
    }
}
