using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class GlobalRouteOptimizationSolverTests
{
    [Fact]
    public async Task SolveAsync_AllRoutes_GeneratesBalancedPlanThenEmergencyReallocation()
    {
        var sourceCity = City("Agudos", 3_000m, -22.46m, -48.99m);
        var problem = new RouteOptimizationProblem(
            RouteOptimizationScope.AllRoutes,
            new DateOnly(2026, 7, 18),
            null,
            Guid.NewGuid(),
            12,
            [
                Route("origem", "AGUDOS NOVA", 10_000m, 13_000m, [sourceCity, City("Bauru", 10_000m, -22.31m, -49.06m)]),
                Route("destino", "BOCAINA", 10_000m, 5_000m, [City("Bocaina", 5_000m, -22.13m, -48.52m)])
            ],
            [Truck("Truck", 10_000m)],
            new OptimizationConstraints(2, 8, 0.05m, 1.00m, 80m),
            "hash");
        var solver = new GlobalRouteOptimizationSolver(new GeographicDistanceMatrixProvider());

        var solution = await solver.SolveAsync(problem, CancellationToken.None);

        Assert.Equal(RouteOptimizationStatus.Completed, solution.Status);
        Assert.Equal(2, solution.Scenarios.Count);
        var scenario = solution.Scenarios[0];
        Assert.Equal(RouteOptimizationActionType.BuildBalancedRoutePlan, scenario.ActionType);
        Assert.Equal(RouteOptimizationActionType.ReallocateCities, solution.Scenarios[1].ActionType);
        var reallocation = Assert.Single(scenario.CityReallocations);
        Assert.Equal(sourceCity.CityId, reallocation.CityId);
        Assert.Equal("AGUDOS NOVA", reallocation.SourceRouteName);
        Assert.Equal("BOCAINA", reallocation.DestinationRouteName);
        Assert.Contains(scenario.Reasons, reason => reason.Code == "BuildBalancedRoutePlan");
    }

    [Fact]
    public async Task SolveAsync_AllRoutes_DoesNotRequireTargetRoute()
    {
        var problem = new RouteOptimizationProblem(
            RouteOptimizationScope.AllRoutes,
            new DateOnly(2026, 7, 18),
            null,
            Guid.NewGuid(),
            12,
            [Route("saudavel", "BOCAINA", 10_000m, 8_000m, [City("Bocaina", 8_000m, -22.13m, -48.52m)])],
            [Truck("Truck", 10_000m)],
            new OptimizationConstraints(2, 8, 0.05m, 1.00m, 80m),
            "hash");
        var solver = new GlobalRouteOptimizationSolver(new GeographicDistanceMatrixProvider());

        var solution = await solver.SolveAsync(problem, CancellationToken.None);

        Assert.Equal(RouteOptimizationStatus.NoChangeRecommended, solution.Status);
        Assert.Single(solution.Scenarios);
    }

    [Fact]
    public async Task SolveAsync_AllRoutes_IgnoresCitiesWithoutCoordinatesWhenOtherCitiesCanBeMoved()
    {
        var sourceCity = City("Agudos", 3_000m, -22.46m, -48.99m);
        var problem = new RouteOptimizationProblem(
            RouteOptimizationScope.AllRoutes,
            new DateOnly(2026, 7, 18),
            null,
            Guid.NewGuid(),
            12,
            [
                Route("origem", "AGUDOS NOVA", 10_000m, 13_000m, [sourceCity, CityWithoutCoordinates("Cidade sem coordenada", 10_000m)]),
                Route("destino", "BOCAINA", 10_000m, 5_000m, [City("Bocaina", 5_000m, -22.13m, -48.52m)])
            ],
            [Truck("Truck", 10_000m)],
            new OptimizationConstraints(2, 8, 0.05m, 1.00m, 80m),
            "hash");
        var solver = new GlobalRouteOptimizationSolver(new GeographicDistanceMatrixProvider());

        var solution = await solver.SolveAsync(problem, CancellationToken.None);

        Assert.Equal(RouteOptimizationStatus.Completed, solution.Status);
        var scenario = solution.Scenarios[0];
        Assert.Single(scenario.CityReallocations);
        Assert.Contains(scenario.Warnings, warning => warning.Contains("sem coordenadas"));
    }

    [Fact]
    public async Task SolveAsync_AllRoutes_DescribesOsrmRoadDistanceWhenProviderUsesRoadRouting()
    {
        var problem = new RouteOptimizationProblem(
            RouteOptimizationScope.AllRoutes,
            new DateOnly(2026, 7, 18),
            null,
            Guid.NewGuid(),
            12,
            [
                Route("origem", "AGUDOS NOVA", 10_000m, 13_000m, [City("Agudos", 3_000m, -22.46m, -48.99m), City("Bauru", 10_000m, -22.31m, -49.06m)]),
                Route("destino", "BOCAINA", 10_000m, 5_000m, [City("Bocaina", 5_000m, -22.13m, -48.52m)])
            ],
            [Truck("Truck", 10_000m)],
            new OptimizationConstraints(2, 8, 0.05m, 1.00m, 80m),
            "hash");
        var solver = new GlobalRouteOptimizationSolver(new FixedRoadDistanceProvider());

        var solution = await solver.SolveAsync(problem, CancellationToken.None);

        var scenario = solution.Scenarios[0];
        Assert.Contains(scenario.Warnings, warning => warning.Contains("OSRM/OpenStreetMap"));
    }

    private static OptimizationRoute Route(
        string routeIdSeed,
        string name,
        decimal capacityKg,
        decimal loadKg,
        IReadOnlyList<OptimizationCity> cities) =>
        new(
            GuidFromSeed(routeIdSeed),
            name,
            "MONDAY",
            GuidFromSeed("truck-" + routeIdSeed),
            "Truck",
            capacityKg,
            loadKg,
            loadKg / capacityKg,
            cities);

    private static OptimizationCity City(
        string name,
        decimal loadKg,
        decimal latitude,
        decimal longitude) =>
        new(GuidFromSeed(name), name, loadKg, new GeoPoint(latitude, longitude), 1);

    private static OptimizationCity CityWithoutCoordinates(string name, decimal loadKg) =>
        new(GuidFromSeed(name), name, loadKg, null, 1);

    private static OptimizationTruckModel Truck(string name, decimal capacityKg) =>
        new(GuidFromSeed(name), name, capacityKg);

    private static Guid GuidFromSeed(string seed)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return new Guid(bytes[..16]);
    }

    private sealed class FixedRoadDistanceProvider : IDistanceMatrixProvider
    {
        public string Method => "OsrmRoadDistance";

        public Task<decimal> GetDistanceKmAsync(
            GeoPoint origin,
            GeoPoint destination,
            CancellationToken cancellationToken) =>
            Task.FromResult(12m);
    }
}
