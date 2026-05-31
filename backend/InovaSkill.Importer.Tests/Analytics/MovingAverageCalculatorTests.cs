using InovaSkill.Importer.Application.Analytics;

namespace InovaSkill.Importer.Tests.Analytics;

public sealed class MovingAverageCalculatorTests
{
    [Fact]
    public void ShouldCalculateAverageUsingConfiguredWindow()
    {
        var result = MovingAverageCalculator.Calculate(new[] { 10m, 20m, 30m, 40m }, window: 3, minimumPeriods: 3);

        Assert.Equal(30m, result);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 3)]
    [InlineData(3, 0)]
    [InlineData(3, -1)]
    public void ShouldReturnNullForInvalidConfiguration(int window, int minimumPeriods)
    {
        var result = MovingAverageCalculator.Calculate(new[] { 10m, 20m, 30m }, window, minimumPeriods);

        Assert.Null(result);
    }

    [Fact]
    public void ShouldReturnNullWhenHistoryIsShorterThanMinimumPeriods()
    {
        var result = MovingAverageCalculator.Calculate(new[] { 10m, 20m }, window: 3, minimumPeriods: 3);

        Assert.Null(result);
    }
}
