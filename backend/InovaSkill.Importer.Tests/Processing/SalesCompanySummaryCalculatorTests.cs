using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Processing;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class SalesCompanySummaryCalculatorTests
{
    [Fact]
    public void Build_CalculatesWeeklyGrowthAndOrdering()
    {
        var rows = new List<CommercialTransaction>
        {
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 12), TotalAmount = 200m, Quantity = 2m, GrossWeightKg = 10m },
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 5), TotalAmount = 100m, Quantity = 1m, GrossWeightKg = 5m },
            new() { CustomerName = "Empresa Y", TransactionDate = new DateTime(2026, 5, 12), TotalAmount = 50m, Quantity = 1m, GrossWeightKg = 2m }
        };

        var result = SalesCompanySummaryCalculator.Build(
            rows,
            SalesSummaryGranularity.Weekly,
            SalesSummarySortBy.Growth,
            new DateTime(2026, 5, 12));

        Assert.Equal(3, result.TotalRecords);
        Assert.Equal(2, result.TotalCompanies);
        Assert.Equal("Empresa X", result.Items[0].CompanyName);
        Assert.Equal(100m, result.Items[0].GrowthPercent);
        Assert.Equal(new DateTime(2026, 5, 11), result.CurrentPeriodStart);
        Assert.Equal(new DateTime(2026, 5, 4), result.PreviousPeriodStart);
        Assert.Equal(250m, result.CurrentPeriodTotalAmount);
        Assert.Equal(100m, result.PreviousPeriodTotalAmount);
        Assert.Equal(150m, result.TotalGrowthPercent);
    }

    [Fact]
    public void Build_UsesDailyGranularityWhenRequested()
    {
        var rows = new List<CommercialTransaction>
        {
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 12), TotalAmount = 120m, Quantity = 1m, GrossWeightKg = 1m },
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 11), TotalAmount = 100m, Quantity = 1m, GrossWeightKg = 1m }
        };

        var result = SalesCompanySummaryCalculator.Build(
            rows,
            SalesSummaryGranularity.Daily,
            SalesSummarySortBy.Growth,
            new DateTime(2026, 5, 12));

        Assert.Single(result.Items);
        Assert.Equal(20m, result.Items[0].GrowthPercent);
        Assert.Equal(120m, result.Items[0].CurrentPeriodAmount);
        Assert.Equal(100m, result.Items[0].PreviousPeriodAmount);
        Assert.Equal(new DateTime(2026, 5, 12), result.CurrentPeriodStart);
        Assert.Equal(new DateTime(2026, 5, 11), result.PreviousPeriodStart);
        Assert.Equal(120m, result.CurrentPeriodTotalAmount);
        Assert.Equal(100m, result.PreviousPeriodTotalAmount);
        Assert.Equal(20m, result.TotalGrowthPercent);
    }

    [Fact]
    public void Build_UsesSelectedWeekEvenWhenThereAreNewerRows()
    {
        var rows = new List<CommercialTransaction>
        {
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 12), TotalAmount = 200m, Quantity = 2m, GrossWeightKg = 10m },
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 5), TotalAmount = 100m, Quantity = 1m, GrossWeightKg = 5m },
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 19), TotalAmount = 999m, Quantity = 9m, GrossWeightKg = 9m }
        };

        var result = SalesCompanySummaryCalculator.Build(
            rows,
            SalesSummaryGranularity.Weekly,
            SalesSummarySortBy.Growth,
            new DateTime(2026, 5, 12));

        Assert.Single(result.Items);
        Assert.Equal(1299m, result.Items[0].TotalAmount);
        Assert.Equal(200m, result.Items[0].CurrentPeriodAmount);
        Assert.Equal(100m, result.Items[0].PreviousPeriodAmount);
        Assert.Equal(100m, result.Items[0].GrowthPercent);
    }

    [Fact]
    public void Build_UsesMonthlyGranularityWhenRequested()
    {
        var rows = new List<CommercialTransaction>
        {
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 15), TotalAmount = 200m, Quantity = 2m, GrossWeightKg = 10m },
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 4, 10), TotalAmount = 100m, Quantity = 1m, GrossWeightKg = 5m }
        };

        var result = SalesCompanySummaryCalculator.Build(
            rows,
            SalesSummaryGranularity.Monthly,
            SalesSummarySortBy.Growth,
            new DateTime(2026, 5, 20));

        Assert.Single(result.Items);
        Assert.Equal(new DateTime(2026, 5, 1), result.CurrentPeriodStart);
        Assert.Equal(new DateTime(2026, 4, 1), result.PreviousPeriodStart);
        Assert.Equal(200m, result.CurrentPeriodTotalAmount);
        Assert.Equal(100m, result.PreviousPeriodTotalAmount);
        Assert.Equal(100m, result.TotalGrowthPercent);
        Assert.Equal(100m, result.Items[0].GrowthPercent);
    }

    [Fact]
    public void Build_PreservesAmountSign_InsteadOfForcingAbsolute()
    {
        var rows = new List<CommercialTransaction>
        {
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 12), TotalAmount = -120m, Quantity = 1m, GrossWeightKg = 1m },
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 5), TotalAmount = -100m, Quantity = 1m, GrossWeightKg = 1m }
        };

        var result = SalesCompanySummaryCalculator.Build(
            rows,
            SalesSummaryGranularity.Weekly,
            SalesSummarySortBy.Growth,
            new DateTime(2026, 5, 12));

        Assert.Equal(-220m, result.TotalAmount);
        Assert.Equal(-120m, result.CurrentPeriodTotalAmount);
        Assert.Equal(-100m, result.PreviousPeriodTotalAmount);
        Assert.Equal(-220m, result.Items[0].TotalAmount);
        Assert.Equal(20m, result.Items[0].GrowthPercent);
    }

    [Fact]
    public void Build_UsesQuantityTimesUnitPrice_WhenTotalAmountIsInconsistent()
    {
        var rows = new List<CommercialTransaction>
        {
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 12), Quantity = 2m, UnitPrice = 10m, TotalAmount = -5000m, GrossWeightKg = 1m },
            new() { CustomerName = "Empresa X", TransactionDate = new DateTime(2026, 5, 5), Quantity = 1m, UnitPrice = 10m, TotalAmount = -4000m, GrossWeightKg = 1m }
        };

        var result = SalesCompanySummaryCalculator.Build(
            rows,
            SalesSummaryGranularity.Weekly,
            SalesSummarySortBy.Growth,
            new DateTime(2026, 5, 12));

        Assert.Equal(30m, result.TotalAmount);
        Assert.Equal(20m, result.CurrentPeriodTotalAmount);
        Assert.Equal(10m, result.PreviousPeriodTotalAmount);
        Assert.Equal(30m, result.Items[0].TotalAmount);
        Assert.Equal(100m, result.Items[0].GrowthPercent);
    }
}
