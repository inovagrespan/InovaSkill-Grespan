using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class DailyRouteOptimizationWeekdaysTests
{
    [Fact]
    public void Read_MissingPropertyMeansEveryDay()
    {
        using var json = JsonDocument.Parse("{}");
        Assert.Null(DailyRouteOptimizationWeekdays.Read(json.RootElement));
    }

    [Fact]
    public void Read_NormalizesAndDeduplicatesRequestedDays()
    {
        using var json = JsonDocument.Parse("{\"weekdays\":[\" monday \",\"MONDAY\",\"thursday\"]}");
        Assert.Equal(["MONDAY", "THURSDAY"], DailyRouteOptimizationWeekdays.Read(json.RootElement));
    }

    [Theory]
    [InlineData("{\"weekdays\":[]}")]
    [InlineData("{\"weekdays\":[\"FERIADO\"]}")]
    [InlineData("{\"weekdays\":\"MONDAY\"}")]
    public void Read_RejectsInvalidContracts(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        Assert.Throws<ArgumentException>(() => DailyRouteOptimizationWeekdays.Read(json.RootElement));
    }
}
