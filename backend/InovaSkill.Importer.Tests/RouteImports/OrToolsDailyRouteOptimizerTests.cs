using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class OrToolsDailyRouteOptimizerTests
{
    [Fact]
    public void Optimize_RentsVehicleWhenOwnFleetWouldExceedHealthyOccupancy()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var firstCity = Guid.NewGuid();
        var secondCity = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [
                new(firstCity, "Bauru", 9_000m, 10, routeId, 1),
                new(secondCity, "Jaú", 2_000m, 4, routeId, 2)
            ],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(firstCity), Point(secondCity)],
            ownCapacity: 10_000m,
            rentalCapacities: [3_300m, 10_300m]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.Equal(DailyRouteOptimizationStatuses.Optimized, result.Status);
        Assert.Single(result.Routes, route => route.IsRental);
        Assert.Contains(result.Routes, route => route.IsRental && route.Name == "Rota atual APOIO");
        Assert.All(result.Routes, route => Assert.InRange(route.Occupancy, 0.01m, 1m));
        Assert.Equal(11_000m, result.Routes.Sum(route => route.LoadKg));
        Assert.Equal(2, result.Routes.SelectMany(route => route.Stops).Select(stop => stop.MunicipalityId).Distinct().Count());
    }

    [Fact]
    public void Optimize_KeepsRouteBelowPhysicalCapacityWithoutOccupancyBand()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [new(cityId, "Marília", 9_800m, 12, routeId, 1)],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId)],
            ownCapacity: 10_000m,
            rentalCapacities: [11_000m]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.Equal(DailyRouteOptimizationStatuses.NoImprovement, result.Status);
        Assert.Equal(9_800m, result.Routes.Sum(route => route.LoadKg));
        Assert.All(result.Routes, route => Assert.False(route.IsRental));
        Assert.Equal(0.98m, result.Routes.Single().Occupancy);
    }

    [Fact]
    public void Optimize_SplitsOversizedMunicipalLoadAcrossVehicles()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [new(cityId, "Marília", 12_000m, 10, routeId, 1)],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId)],
            ownCapacity: 10_000m,
            rentalCapacities: [10_000m]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.Equal(DailyRouteOptimizationStatuses.Optimized, result.Status);
        Assert.Equal(2, result.Routes.Count);
        Assert.Equal(12_000m, result.Routes.Sum(route => route.LoadKg));
        Assert.Equal(10, result.Routes.SelectMany(route => route.Stops).Sum(stop => stop.Deliveries));
        Assert.All(result.Routes, route => Assert.InRange(route.Occupancy, 0.01m, 1m));
        Assert.All(result.Routes.SelectMany(route => route.Stops), stop => Assert.Equal(cityId, stop.MunicipalityId));
    }

    [Fact]
    public void Optimize_AcceptsExactlyNinetyFivePercentAndKeepsEqualCurrentRoute()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [new(cityId, "Lins", 9_500m, 8, routeId, 1)],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId)],
            ownCapacity: 10_000m,
            rentalCapacities: [10_300m]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.Equal(DailyRouteOptimizationStatuses.NoImprovement, result.Status);
        Assert.Equal(0.95m, result.Proposed.MaximumOccupancy);
        Assert.Equal(9_500m, result.Routes.Single().LoadKg);
        Assert.Equal(1, result.Proposed.VehiclesUsed);
        Assert.Equal(0, result.Proposed.RentalVehicles);
        Assert.Equal(result.Current.DistanceMeters, result.Proposed.DistanceMeters);
        Assert.Equal(result.Current.DurationSeconds, result.Proposed.DurationSeconds);
    }

    [Fact]
    public void Optimize_AllowsOwnRouteBelowFormerMinimumOccupancy()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [new(cityId, "Garça", 1_000m, 2, routeId, 1)],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId)],
            ownCapacity: 10_000m,
            rentalCapacities: [3_300m]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.Equal(DailyRouteOptimizationStatuses.NoImprovement, result.Status);
        Assert.Equal(0.10m, Assert.Single(result.Routes).Occupancy);
    }

    [Fact]
    public void Optimize_DoesNotRentOnlyToImproveOccupancy()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [new(cityId, "Garça", 2_000m, 3, routeId, 1)],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId)],
            ownCapacity: 10_000m,
            rentalCapacities: [3_300m]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.Equal(DailyRouteOptimizationStatuses.NoImprovement, result.Status);
        var route = Assert.Single(result.Routes);
        Assert.False(route.IsRental);
        Assert.InRange(route.Occupancy, 0.01m, 1m);
    }

    [Fact]
    public void Optimize_DoesNotRearrangeOnlyToReachAnOccupancyBand()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var firstCity = Guid.NewGuid();
        var secondCity = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [
                new(firstCity, "Cidade A", 1_000m, 2, routeId, 1),
                new(secondCity, "Cidade B", 1_000m, 3, routeId, 2)
            ],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(firstCity), Point(secondCity)],
            ownCapacity: 10_000m,
            rentalCapacities: [3_300m]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.Equal(DailyRouteOptimizationStatuses.NoImprovement, result.Status);
        var route = Assert.Single(result.Routes);
        Assert.False(route.IsRental);
        Assert.Equal(2_000m, route.LoadKg);
        Assert.Equal(2, route.Stops.Count);
        Assert.InRange(route.Occupancy, 0.01m, 1m);
    }

    [Fact]
    public void Optimize_DoesNotRejectEfficientRouteBecauseOfDuration()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [new(cityId, "Cidade distante", 7_000m, 3, routeId, 1)],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId)],
            ownCapacity: 10_000m,
            rentalCapacities: [10_000m]) with
        {
            DurationsSeconds = [[0m, 20_000m], [20_000m, 0m]]
        };

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.NotEqual(DailyRouteOptimizationStatuses.Infeasible, result.Status);
        Assert.Single(result.Routes);
        Assert.Equal(40_000m, result.Routes.Single().DurationSeconds);
    }

    [Fact]
    public void Optimize_SelectsSmallestRentalVehicleThatFitsTheLoad()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [new(cityId, "Garça", 3_000m, 3, routeId, 1)],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId)],
            ownCapacity: 2_000m,
            rentalCapacities: [3_300m, 5_000m]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        var support = Assert.Single(result.Routes);
        Assert.True(support.IsRental);
        Assert.Equal(3_300m, support.CapacityKg);
        Assert.Equal("Rota atual APOIO", support.Name);
        Assert.Equal(400m, support.RentalDailyCostMinimum);
        Assert.Equal(600m, support.RentalDailyCostMaximum);
        Assert.Equal(200m, support.TankCapacityLiters);
        Assert.InRange(support.TankUsagePercent, 0m, 90m);
        Assert.True(support.RemainingAutonomyKm > 0);
    }

    [Theory]
    [InlineData(4_000, 5_000, 650, 900, 300)]
    [InlineData(7_000, 10_000, 900, 1_300, 300)]
    public void Optimize_ReportsRentalDailyRangeAndTankByVehicleClass(
        decimal load, decimal capacity, decimal expectedMinimum, decimal expectedMaximum, decimal expectedTank)
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [new(cityId, "Garça", load, 3, routeId, 1)],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId)],
            ownCapacity: load - 1m,
            rentalCapacities: [capacity]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        var rental = Assert.Single(result.Routes);
        Assert.True(rental.IsRental);
        Assert.Equal(expectedMinimum, rental.RentalDailyCostMinimum);
        Assert.Equal(expectedMaximum, rental.RentalDailyCostMaximum);
        Assert.Equal(expectedTank, rental.TankCapacityLiters);
    }

    [Fact]
    public void Optimize_AllowsRentalSupportAtExactlyFiftyPercent()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var mainCity = Guid.NewGuid();
        var supportCity = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [
                new(mainCity, "Cidade principal", 9_500m, 10, routeId, 1),
                new(supportCity, "Cidade de apoio", 1_650m, 2, routeId, 2)
            ],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(mainCity), Point(supportCity)],
            ownCapacity: 10_000m,
            rentalCapacities: [3_300m]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        var support = Assert.Single(result.Routes, route => route.IsRental);
        Assert.Equal(0.50m, support.Occupancy);
        Assert.All(result.Routes.Where(route => !route.IsRental), route =>
            Assert.InRange(route.Occupancy, 0.01m, 1m));
    }

    [Fact]
    public void Optimize_UsesAvailableOwnVehicleBeforeEquivalentRental()
    {
        var importId = Guid.NewGuid();
        var criticalRouteId = Guid.NewGuid();
        var idleRouteId = Guid.NewGuid();
        var mainCity = Guid.NewGuid();
        var nearbyCity = Guid.NewGuid();
        var points = new[] { Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(mainCity), Point(nearbyCity) };
        IReadOnlyList<IReadOnlyList<decimal>> matrix = [[0m, 100m, 110m], [100m, 0m, 10m], [110m, 10m, 0m]];
        var problem = new DailyRouteOptimizationProblem(importId, "MONDAY", 6.90m, points, matrix, matrix,
            [
                new(mainCity, "Principal", 9_000m, 10, criticalRouteId, 1),
                new(nearbyCity, "Vizinha", 2_000m, 3, criticalRouteId, 2)
            ],
            [
                new(criticalRouteId, Guid.NewGuid(), "Rota crítica", "Truck", 10_000m, false),
                new(idleRouteId, Guid.NewGuid(), "Rota ociosa", "Accelo", 3_300m, false)
            ],
            [new(null, Guid.NewGuid(), "Accelo alugado", "Accelo", 3_300m, true)]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.DoesNotContain(result.Routes, route => route.IsRental);
        Assert.Contains(result.Routes, route => route.OriginalRouteId == idleRouteId && route.LoadKg == 2_000m);
    }

    [Fact]
    public void Optimize_RejectsRouteBeyondTankAutonomyWithReserve()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [new(cityId, "Cidade muito distante", 7_000m, 3, routeId, 1)],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId)],
            ownCapacity: 10_000m,
            rentalCapacities: [10_000m]) with
        {
            DistancesMeters = [[0m, 500_000m], [500_000m, 0m]]
        };

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.Equal(DailyRouteOptimizationStatuses.Infeasible, result.Status);
        Assert.Empty(result.Routes);
    }

    [Fact]
    public void Optimize_DoesNotRentUnderutilizedSupportBecauseOfTravelTime()
    {
        var importId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var nearCity = Guid.NewGuid();
        var distantCity = Guid.NewGuid();
        var problem = Problem(importId, routeId,
            [
                new(nearCity, "Cidade principal", 7_000m, 8, routeId, 1),
                new(distantCity, "Cidade de apoio", 1_000m, 1, routeId, 2)
            ],
            [Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(nearCity), Point(distantCity)],
            ownCapacity: 10_000m,
            rentalCapacities: [3_300m]) with
        {
            DurationsSeconds = [
                [0m, 1_000m, 1_000m],
                [1_000m, 0m, 40_000m],
                [1_000m, 40_000m, 0m]
            ]
        };

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        Assert.DoesNotContain(result.Routes, route => route.IsRental);
        var route = Assert.Single(result.Routes);
        Assert.InRange(route.Occupancy, 0.01m, 1m);
        Assert.True(route.DurationSeconds > 19_000m);
    }

    [Fact]
    public void Optimize_PrefersMoreFuelEfficientVehicleForTheSameLoad()
    {
        var importId = Guid.NewGuid();
        var originalRouteId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var points = new[] { Point(Guid.NewGuid(), OsrmMatrixPointTypes.Depot), Point(cityId) };
        IReadOnlyList<IReadOnlyList<decimal>> matrix = [[0m, 100_000m], [100_000m, 0m]];
        var problem = new DailyRouteOptimizationProblem(importId, "MONDAY", 6.90m, points, matrix, matrix,
            [new(cityId, "Bauru", 7_000m, 8, originalRouteId, 1)],
            [
                new(originalRouteId, Guid.NewGuid(), "Rota Toco", "Toco", 8_000m, false),
                new(Guid.NewGuid(), Guid.NewGuid(), "Rota Truck", "Truck", 10_000m, false)
            ],
            [new(null, Guid.NewGuid(), "Alugado", "Truck", 10_000m, true)]);

        var result = new OrToolsDailyRouteOptimizer().Optimize(problem, "TEST_MATRIX");

        var route = Assert.Single(result.Routes);
        Assert.Equal("Toco", route.VehicleType);
        Assert.False(route.IsRental);
        Assert.Equal(48.19m, route.EstimatedFuelLiters);
        Assert.Equal(300m, route.TankCapacityLiters);
        Assert.Equal(16.1m, route.TankUsagePercent);
        Assert.Equal(920.5m, route.RemainingAutonomyKm);
    }

    private static DailyRouteOptimizationProblem Problem(
        Guid importId, Guid routeId, IReadOnlyList<DailyOptimizationStop> stops,
        IReadOnlyList<OsrmMatrixPoint> points, decimal ownCapacity, decimal[] rentalCapacities)
    {
        var size = points.Count;
        var matrix = Enumerable.Range(0, size)
            .Select(row => (IReadOnlyList<decimal>)Enumerable.Range(0, size)
                .Select(column => row == column ? 0m : 100m + Math.Abs(row - column)).ToArray()).ToArray();
        return new(importId, "MONDAY", 6.90m, points, matrix, matrix, stops,
            [new(routeId, Guid.NewGuid(), "Rota atual", "Truck", ownCapacity, false)],
            rentalCapacities.Select((capacity, index) => new DailyOptimizationVehicle(
                null, Guid.NewGuid(), $"Alugado {index}", $"Tipo {capacity}", capacity, true)).ToArray());
    }

    private static OsrmMatrixPoint Point(Guid id, string type = OsrmMatrixPointTypes.Municipality) =>
        new(id, type, -22m, -49m);
}
