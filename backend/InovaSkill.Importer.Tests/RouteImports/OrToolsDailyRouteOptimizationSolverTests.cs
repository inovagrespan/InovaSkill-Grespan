using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class OrToolsDailyRouteOptimizationSolverTests
{
    private readonly OrToolsDailyRouteOptimizationSolver solver = new(
        Options.Create(new RouteOptimizationOptions { SolverTimeoutSeconds = 1, ServiceTimePerStopMinutes = 0 }));

    [Fact]
    public void AddsAceloInsteadOfTocoForTwelveTonnesWhenBlocksFit()
    {
        var problem = Problem([9_000_000, 3_000_000], [Vehicle("Truck", 10_300_000)],
            [Vehicle("Acelo", 3_300_000, true), Vehicle("Toco", 7_700_000, true)]);

        var result = solver.Solve(problem);

        Assert.Equal(DailyRouteOptimizationStatuses.Optimized, result.Status);
        var additional = Assert.Single(result.Vehicles, vehicle => vehicle.Vehicle.IsAdditional);
        Assert.Equal("Acelo", additional.Vehicle.VehicleTypeName);
        Assert.All(result.Vehicles, vehicle => Assert.True(vehicle.LoadGrams <= vehicle.Vehicle.CapacityGrams));
        Assert.Equal(problem.Blocks.Sum(block => block.WeightGrams), result.Vehicles.Sum(vehicle => vehicle.LoadGrams));
    }

    [Fact]
    public void MinimizesAdditionalVehicleCountBeforeAdditionalCapacity()
    {
        var problem = Problem([10_000_000, 3_250_000, 3_250_000], [Vehicle("Truck", 10_300_000)],
            [Vehicle("Acelo", 3_300_000, true), Vehicle("Toco", 7_700_000, true)]);

        var result = solver.Solve(problem);

        var additional = Assert.Single(result.Vehicles, vehicle => vehicle.Vehicle.IsAdditional);
        Assert.Equal("Toco", additional.Vehicle.VehicleTypeName);
    }

    [Fact]
    public void DoesNotAddVehicleOnlyToReduceDistance()
    {
        var problem = Problem([4_000_000, 4_000_000], [Vehicle("Truck", 10_300_000)],
            [Vehicle("Acelo", 3_300_000, true), Vehicle("Toco", 7_700_000, true)]);

        var result = solver.Solve(problem);

        Assert.DoesNotContain(result.Vehicles, vehicle => vehicle.Vehicle.IsAdditional);
        Assert.Equal(2, result.Vehicles.SelectMany(vehicle => vehicle.Stops).Select(stop => stop.BlockIndex).Distinct().Count());
    }

    [Fact]
    public void RemovesUnusedExistingVehiclesFromSuggestedFleet()
    {
        var problem = Problem([3_000_000],
            [Vehicle("Acelo", 3_300_000), Vehicle("Truck", 10_300_000)], []);

        var result = solver.Solve(problem);

        var vehicle = Assert.Single(result.Vehicles);
        Assert.Equal(3_000_000, vehicle.LoadGrams);
        Assert.DoesNotContain(result.Vehicles, item => item.LoadGrams == 0);
    }

    [Fact]
    public void ReturnsInfeasibleWhenNoFleetCanCarryAnIndivisibleBlock()
    {
        var problem = Problem([11_000_000], [Vehicle("Truck", 10_300_000)],
            [Vehicle("Acelo", 3_300_000, true), Vehicle("Toco", 7_700_000, true)]);

        var result = solver.Solve(problem);

        Assert.Equal(DailyRouteOptimizationStatuses.Infeasible, result.Status);
        Assert.Empty(result.Vehicles);
    }

    [Fact]
    public void RoutesSplitLoadsFromSameMunicipalityInDifferentVehicles()
    {
        var problem = Problem([10_300_000, 700_000], [Vehicle("Truck", 10_300_000)],
            [Vehicle("Acelo", 3_300_000, true)]);
        var municipalityId = problem.Blocks[0].MunicipalityId;
        problem = problem with
        {
            Blocks = [problem.Blocks[0], problem.Blocks[1] with
                { LocationId = problem.Blocks[0].LocationId, MunicipalityId = municipalityId }],
            Matrix = problem.Matrix with
            {
                Points = [problem.Matrix.Points[0], problem.Matrix.Points[1]],
                DistancesMeters = [new decimal[] { 0, 100 }, new decimal[] { 100, 0 }],
                DurationsSeconds = [new decimal[] { 0, 100 }, new decimal[] { 100, 0 }]
            }
        };

        var result = solver.Solve(problem);

        Assert.Equal(DailyRouteOptimizationStatuses.Optimized, result.Status);
        Assert.Equal(2, result.Vehicles.Count(vehicle => vehicle.LoadGrams > 0));
        Assert.Equal(11_000_000, result.Vehicles.Sum(vehicle => vehicle.LoadGrams));
        Assert.All(result.Vehicles, vehicle => Assert.True(vehicle.LoadGrams <= vehicle.Vehicle.CapacityGrams));
    }

    [Fact]
    public void UsesDurationAsTieBreakerWhenTotalDistanceIsEqual()
    {
        var problem = Problem([1_000_000, 1_000_000], [Vehicle("Truck", 10_300_000)], []);
        IReadOnlyList<IReadOnlyList<decimal>> distances =
        [new decimal[] { 0, 1, 1 }, new decimal[] { 1, 0, 1 }, new decimal[] { 1, 1, 0 }];
        IReadOnlyList<IReadOnlyList<decimal>> durations =
        [new decimal[] { 0, 1, 10 }, new decimal[] { 10, 0, 1 }, new decimal[] { 1, 10, 0 }];
        problem = problem with { Matrix = problem.Matrix with { DistancesMeters = distances, DurationsSeconds = durations } };

        var result = solver.Solve(problem);

        Assert.Equal([0, 1], Assert.Single(result.Vehicles).Stops.Select(stop => stop.BlockIndex).ToArray());
        Assert.Equal(3, result.TotalDistanceMeters);
        Assert.Equal(3, result.TotalDurationSeconds);
    }

    [Fact]
    public void KeepsFeasibleFleetSeedWithOneSecondSolverLimit()
    {
        var problem = Problem(
            Enumerable.Repeat(1_000_000L, 25).ToArray(),
            [Vehicle("Truck A", 10_300_000), Vehicle("Truck B", 10_300_000)],
            [Vehicle("Acelo", 3_300_000, true), Vehicle("Toco", 7_700_000, true)]);

        var result = solver.Solve(problem);

        Assert.Equal(DailyRouteOptimizationStatuses.Optimized, result.Status);
        Assert.Equal(25, result.Vehicles.SelectMany(vehicle => vehicle.Stops).Count());
        Assert.Equal(25_000_000, result.Vehicles.Sum(vehicle => vehicle.LoadGrams));
        Assert.All(result.Vehicles, vehicle => Assert.InRange(vehicle.LoadGrams, 0, vehicle.Vehicle.CapacityGrams));
    }

    [Fact]
    public void SplitsFleetWhenStopServiceWouldExceedEightHourWorkday()
    {
        var constrainedSolver = new OrToolsDailyRouteOptimizationSolver(Options.Create(
            new RouteOptimizationOptions
            {
                SolverTimeoutSeconds = 1,
                MaximumRouteDurationHours = 8,
                ServiceTimePerStopMinutes = 60
            }));
        var problem = Problem(Enumerable.Repeat(1_000_000L, 10).ToArray(),
            [Vehicle("Truck", 10_300_000)], [Vehicle("Truck", 10_300_000, true)]);

        var result = constrainedSolver.Solve(problem);

        Assert.Equal(DailyRouteOptimizationStatuses.Optimized, result.Status);
        Assert.True(result.Vehicles.Count >= 2);
        Assert.All(result.Vehicles, vehicle =>
            Assert.InRange(vehicle.DurationSeconds, 0, DailyRouteOptimizationPolicy.MaximumRouteDurationSeconds));
        Assert.Equal(3_600, result.ServiceDurationSeconds);
        Assert.Equal(result.Vehicles.Sum(vehicle => vehicle.DurationSeconds), result.TotalDurationSeconds);
    }

    [Fact]
    public void AllowsPreferredEightHourRouteToUseTheHardFifteenHourLimit()
    {
        var constrainedSolver = new OrToolsDailyRouteOptimizationSolver(Options.Create(
            new RouteOptimizationOptions
            {
                SolverTimeoutSeconds = 1,
                MaximumRouteDurationHours = DailyRouteOptimizationPolicy.MaximumRouteDurationHours,
                ServiceTimePerStopMinutes = 60
            }));
        var problem = Problem(Enumerable.Repeat(1_000_000L, 9).ToArray(),
            [Vehicle("Truck", 10_300_000)], []);

        var result = constrainedSolver.Solve(problem);

        Assert.Equal(DailyRouteOptimizationStatuses.Optimized, result.Status);
        var route = Assert.Single(result.Vehicles);
        Assert.True(route.DurationSeconds > DailyRouteOptimizationPolicy.PreferredRouteDurationSeconds);
        Assert.InRange(route.DurationSeconds, DailyRouteOptimizationPolicy.PreferredRouteDurationSeconds + 1,
            DailyRouteOptimizationPolicy.MaximumRouteDurationSeconds);
        Assert.Equal(DailyRouteOptimizationPolicy.MaximumRouteDurationSeconds, result.MaximumRouteDurationSeconds);
    }

    [Fact]
    public void RejectsHardLimitAboveFifteenHours()
    {
        Assert.Throws<InvalidOperationException>(() => new OrToolsDailyRouteOptimizationSolver(Options.Create(
            new RouteOptimizationOptions { MaximumRouteDurationHours = 16 })));
    }

    [Fact]
    public void BuildsScalableFeasibleSeedForCustomerLevelProblem()
    {
        var weights = Enumerable.Repeat(10_000L,
            DailyRouteOptimizationPolicy.ExactFleetSelectionMaximumBlocks + 1).ToArray();
        var problem = Problem(weights, [Vehicle("Truck", 10_300_000)],
            [Vehicle("Acelo", 3_300_000, true)]);

        var result = solver.Solve(problem);

        Assert.Equal(DailyRouteOptimizationStatuses.Optimized, result.Status);
        Assert.Equal(weights.Length, result.Vehicles.SelectMany(vehicle => vehicle.Stops).Count());
        Assert.Equal(weights.Sum(), result.Vehicles.Sum(vehicle => vehicle.LoadGrams));
        Assert.All(result.Vehicles, vehicle =>
            Assert.InRange(vehicle.DurationSeconds, 0, DailyRouteOptimizationPolicy.MaximumRouteDurationSeconds));
    }

    private static RouteOptimizationProblem Problem(long[] weights,
        RouteOptimizationVehicleInput[] existing, RouteOptimizationVehicleInput[] additional)
    {
        var blocks = weights.Select((weight, index) => new RouteOptimizationBlock(
            Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}"), $"Cidade {index + 1}", weight)).ToArray();
        var points = new[] { new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, 0, 0) }
            .Concat(blocks.Select(block => new OsrmMatrixPoint(block.MunicipalityId, OsrmMatrixPointTypes.Municipality, 0, 0))).ToArray();
        var size = points.Length;
        var distances = Enumerable.Range(0, size).Select(row => (IReadOnlyList<decimal>)
            Enumerable.Range(0, size).Select(column => row == column ? 0m : (decimal)(100 + row * 10 + column)).ToArray()).ToArray();
        return new("MONDAY", blocks, existing, additional,
            new OsrmTableResult("TEST", points, distances, distances));
    }

    private static RouteOptimizationVehicleInput Vehicle(string name, long capacity, bool additional = false) =>
        new(additional ? null : Guid.NewGuid(), Guid.NewGuid(), name, capacity, additional);
}
