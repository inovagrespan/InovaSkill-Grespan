using InovaSkill.Importer.Application.Analytics;

namespace InovaSkill.Importer.Tests.Analytics;

public class FinanceDashboardCalculatorTests
{
    private static readonly FinanceDashboardTransaction[] Sample =
    [
        new("Cliente A", new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 1000m, "NF-1", 10m),
        new("Cliente A", new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), 2000m, "NF-2", 20m),
        new("Cliente B", new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), 4000m, "NF-3", 40m),
        new("Cliente B", new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), 500m, "NF-3", 5m),
    ];

    [Fact]
    public void BuildItems_FiltersByCustomerAndDateRange()
    {
        var result = FinanceDashboardCalculator.BuildItems(
            Sample,
            "Cliente A",
            new DateTime(2026, 2, 1),
            new DateTime(2026, 2, 28),
            allTime: false);

        var item = Assert.Single(result);
        Assert.Equal("Cliente A", item.Customer);
        Assert.Equal(2000m, item.Revenue);
        Assert.Equal(1, item.Orders);
        Assert.Equal(20m, item.Quantity);
    }

    [Fact]
    public void BuildItems_GroupsDailyRevenueAndDistinctOrders()
    {
        var result = FinanceDashboardCalculator.BuildItems(Sample, "", null, null, allTime: true);

        Assert.Collection(
            result,
            first =>
            {
                Assert.Equal("Cliente A", first.Customer);
                Assert.Equal(new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), first.Date);
                Assert.Equal(1000m, first.Revenue);
                Assert.Equal(1, first.Orders);
                Assert.Equal(10m, first.Quantity);
            },
            second =>
            {
                Assert.Equal("Cliente A", second.Customer);
                Assert.Equal(2000m, second.Revenue);
            },
            third =>
            {
                Assert.Equal("Cliente B", third.Customer);
                Assert.Equal(4500m, third.Revenue);
                Assert.Equal(1, third.Orders);
                Assert.Equal(45m, third.Quantity);
            });
    }

    [Fact]
    public void BuildSummary_CalculatesAverageTicketWithoutDivisionError()
    {
        var items = FinanceDashboardCalculator.BuildItems(Sample, "", null, null, allTime: true);

        var summary = FinanceDashboardCalculator.BuildSummary(items);

        Assert.Equal(7500m, summary.TotalRevenue);
        Assert.Equal(3, summary.TotalOrders);
        Assert.Equal(75m, summary.TotalQuantity);
        Assert.Equal(2500m, summary.AverageTicket);
    }

    [Fact]
    public void BuildRevenueTrend_GroupsMonthlyWeeklyAndYearly()
    {
        var items = FinanceDashboardCalculator.BuildItems(Sample, "", null, null, allTime: true);

        Assert.Equal(
            [
                new FinanceRevenueTrendPoint("2026-01", "jan", 1000m),
                new FinanceRevenueTrendPoint("2026-02", "fev", 6500m),
            ],
            FinanceDashboardCalculator.BuildRevenueTrend(items, "monthly"));

        Assert.Equal(
            [
                new FinanceRevenueTrendPoint("2026-01-05", "Sem 05/01", 1000m),
                new FinanceRevenueTrendPoint("2026-02-09", "Sem 09/02", 6500m),
            ],
            FinanceDashboardCalculator.BuildRevenueTrend(items, "weekly"));

        Assert.Equal(
            [new FinanceRevenueTrendPoint("2026", "2026", 7500m)],
            FinanceDashboardCalculator.BuildRevenueTrend(items, "yearly"));
    }

    [Fact]
    public void BuildCustomerRevenueRanking_SortsByRevenueAndAlphabeticalTieBreak()
    {
        var items = new[]
        {
            new FinanceDashboardItem("Cliente B", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 3000m, 1, 10m),
            new FinanceDashboardItem("Cliente A", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 3000m, 1, 10m),
            new FinanceDashboardItem("Cliente C", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1000m, 1, 10m),
        };

        var result = FinanceDashboardCalculator.BuildCustomerRevenueRanking(items);

        Assert.Equal(
            [
                new FinanceCustomerRevenuePoint("Cliente A", 3000m),
                new FinanceCustomerRevenuePoint("Cliente B", 3000m),
                new FinanceCustomerRevenuePoint("Cliente C", 1000m),
            ],
            result);
    }
}
