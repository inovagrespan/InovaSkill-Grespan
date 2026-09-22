using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class DailyRouteOptimizationReadinessEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsEveryBlockingCategoryAndCombinedIssuesForSameStop()
    {
        var route = Route(capacity: null);
        var zeroWithoutMunicipality = Entry("SEM MUNICÍPIO", 0);
        var negativeWithMissingCoordinate = Entry("PESO NEGATIVO", -1, MunicipalityWithoutCoordinate());
        route.Entries = [zeroWithoutMunicipality, negativeWithMissingCoordinate];

        var issues = DailyRouteOptimizationReadinessEvaluator.Evaluate([route]);

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, issue => issue.Code == DailyRouteOptimizationIssueCodes.VehicleCapacityMissing);
        Assert.Contains(issues, issue => issue.Code == DailyRouteOptimizationIssueCodes.InvalidStopWeight &&
                                         issue.RouteEntryId == negativeWithMissingCoordinate.Id &&
                                         issue.Message.Contains("negativo", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("1 peso inválido, 1 capacidade ausente.",
            DailyRouteOptimizationReadinessEvaluator.Summarize(issues));
    }

    [Fact]
    public void Evaluate_ExcludedStopsDoNotBlockAndZeroDemandIsIgnored()
    {
        var route = Route(10_000);
        var zero = Entry("ZERO", 0);
        zero.IsExcludedFromOptimization = true;
        var negative = Entry("NEGATIVO", -5);
        negative.IsExcludedFromOptimization = true;
        route.Entries =
        [
            zero,
            negative
        ];

        var issues = DailyRouteOptimizationReadinessEvaluator.Evaluate([route]);

        Assert.DoesNotContain(issues, issue => issue.RouteEntryId == route.Entries.First().Id);
        Assert.DoesNotContain(issues, issue => issue.RouteEntryId == route.Entries.Last().Id);
    }

    private static Route Route(decimal? capacity)
    {
        var vehicle = new VehicleType { Id = Guid.NewGuid(), Name = "Truck", CapacityKg = capacity };
        return new Route
        {
            Id = Guid.NewGuid(), Name = "ROTA", Weekday = "MONDAY",
            VehicleTypeId = vehicle.Id, VehicleType = vehicle, VehicleCapacityKgSnapshot = capacity ?? 0
        };
    }

    private static RouteEntry Entry(string name, decimal weight, Municipality? municipality = null) => new()
    {
        Id = Guid.NewGuid(), Name = name, AveragePerDay = weight,
        MunicipalityId = municipality?.Id, Municipality = municipality
    };

    private static Municipality MunicipalityWithoutCoordinate() => new()
    {
        Id = Guid.NewGuid(), Name = "MARÍLIA", NormalizedName = "MARILIA", StateCode = "SP"
    };
}
