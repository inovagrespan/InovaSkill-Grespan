using InovaSkill.Importer.Infrastructure.Processing.TransformRules;

namespace InovaSkill.Importer.Tests.Processing;

public class BrazilianCurrencyRuleTests
{
    [Theory]
    [InlineData("R$ 1.250,30", 1250.30)]
    [InlineData("1.250,30", 1250.30)]
    [InlineData("1250,30", 1250.30)]
    [InlineData("3.32", 3.32)]
    [InlineData("1,250.30", 1250.30)]
    [InlineData("-1.250,30", -1250.30)]
    [InlineData("1.234", 1234)]
    [InlineData("12.345", 12345)]
    public void Apply_ShouldParseBrazilianCurrency(string input, decimal expected)
    {
        var rule = new BrazilianCurrencyRule();

        var result = rule.Apply(input, "{\"culture\":\"pt-BR\"}");

        Assert.Equal(expected, Assert.IsType<decimal>(result));
    }

    [Fact]
    public void Apply_ShouldKeepInternationalDecimalWhenDotHasTwoDecimalDigits()
    {
        var rule = new BrazilianCurrencyRule();

        var result = rule.Apply("3.32", null);

        Assert.Equal(3.32m, Assert.IsType<decimal>(result));
    }

    [Fact]
    public void Apply_ShouldRejectNegativeWhenConfigured()
    {
        var rule = new BrazilianCurrencyRule();

        var error = Assert.Throws<InvalidOperationException>(() =>
            rule.Apply("-1.250,30", "{\"allowNegative\":false}"));

        Assert.Contains("negativo", error.Message);
    }

    [Fact]
    public void Apply_ShouldRoundDecimalPlacesWhenConfigured()
    {
        var rule = new BrazilianCurrencyRule();

        var result = rule.Apply("1.234,567", "{\"decimalSeparator\":\",\",\"thousandSeparator\":\".\",\"decimalPlaces\":2}");

        Assert.Equal(1234.57m, Assert.IsType<decimal>(result));
    }

    [Fact]
    public void Apply_ShouldRejectContradictoryBusinessLimits()
    {
        var rule = new BrazilianCurrencyRule();

        var error = Assert.Throws<InvalidOperationException>(() =>
            rule.Apply("10.000,01", "{\"decimalSeparator\":\",\",\"thousandSeparator\":\".\",\"maxValue\":10000}"));

        Assert.Contains("maximo", error.Message);
    }
}
