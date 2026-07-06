using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class CustomerProjectionCalculatorTests
{
    [Fact]
    public void Calculate_PerfectLinearGrowth_ProjectsTrendAndExactRange()
    {
        var start = new DateOnly(2025, 1, 1);
        var observations = Enumerable.Range(0, 12)
            .Select(index => new CustomerMonthlyObservation(
                start.AddMonths(index),
                100 + 10 * index,
                1_000 + 100 * index))
            .ToArray();

        var result = CustomerProjectionCalculator.Calculate(observations);

        Assert.Equal(10m, result.Weight.MonthlyChange);
        Assert.Equal(100m, result.Revenue.MonthlyChange);
        Assert.Equal(1m, result.Weight.RSquared);
        Assert.Equal("HIGH", result.Weight.Quality);
        Assert.Equal(220m, result.Weight.Forecast[0].Forecast);
        Assert.Equal(result.Weight.Forecast[0].Forecast, result.Weight.Forecast[0].LowerBound);
        Assert.Equal(result.Weight.Forecast[0].Forecast, result.Weight.Forecast[0].UpperBound);
        Assert.Equal(2_200m, result.Revenue.Forecast[0].Forecast);
    }

    [Fact]
    public void Calculate_DecliningSeries_NeverReturnsNegativeForecastOrBounds()
    {
        var observations = Enumerable.Range(0, 12)
            .Select(index => new CustomerMonthlyObservation(
                new DateOnly(2025, 1, 1).AddMonths(index),
                Math.Max(0, 55 - 5 * index),
                Math.Max(0, 110 - 10 * index)))
            .ToArray();

        var result = CustomerProjectionCalculator.Calculate(observations);

        Assert.True(result.Weight.MonthlyChange < 0);
        Assert.All(result.Weight.Forecast, point =>
        {
            Assert.True(point.Forecast >= 0);
            Assert.True(point.LowerBound >= 0);
            Assert.True(point.UpperBound >= 0);
        });
    }

    [Fact]
    public void Calculate_SparseActivity_ReportsInsufficientInsteadOfFalseConfidence()
    {
        var observations = Enumerable.Range(0, 12)
            .Select(index => new CustomerMonthlyObservation(
                new DateOnly(2025, 1, 1).AddMonths(index),
                index is 3 or 8 ? 100 : 0,
                index is 3 or 8 ? 1_000 : 0))
            .ToArray();

        var result = CustomerProjectionCalculator.Calculate(observations);

        Assert.Equal("INSUFFICIENT", result.Weight.Quality);
        Assert.Equal(2, result.Weight.ActiveMonths);
        Assert.Equal("INSUFFICIENT", result.Revenue.Quality);
    }

    [Fact]
    public void Calculate_RejectsMissingOrNonConsecutiveMonths()
    {
        Assert.Throws<ArgumentException>(() => CustomerProjectionCalculator.Calculate([]));
        var observations = Enumerable.Range(0, 12)
            .Select(index => new CustomerMonthlyObservation(
                new DateOnly(2025, 1, 1).AddMonths(index + (index == 11 ? 1 : 0)), 1, 1))
            .ToArray();
        Assert.Throws<ArgumentException>(() => CustomerProjectionCalculator.Calculate(observations));
    }
}
