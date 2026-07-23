using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class SingleRouteOptimizationSolverTests
{
    [Fact]
    public async Task SolveAsync_ReallocatesCityWhenItRelievesOverloadWithoutMakingDestinationCritical()
    {
        var sourceCity = City("Agudos", 3_000m, -22.46m, -48.99m);
        var problem = Problem(
            [
                Route("origem", "AGUDOS NOVA", 10_000m, 13_000m, [sourceCity, City("Bauru", 10_000m, -22.31m, -49.06m)]),
                Route("destino", "BOCAINA", 10_000m, 5_000m, [City("Bocaina", 5_000m, -22.13m, -48.52m)])
            ],
            [Truck("Truck", 10_000m), Truck("Truck maior", 15_000m)]);

        var solver = new SingleRouteOptimizationSolver(new GeographicDistanceMatrixProvider());

        var solution = await solver.SolveAsync(problem, CancellationToken.None);

        Assert.Equal(RouteOptimizationStatus.Completed, solution.Status);
        var scenario = Assert.Single(solution.Scenarios);
        Assert.Equal(RouteOptimizationActionType.ReallocateCities, scenario.ActionType);
        Assert.Equal(RouteOptimizationConfidence.Medium, scenario.Confidence);
        var reallocation = Assert.Single(scenario.CityReallocations);
        Assert.Equal(sourceCity.CityId, reallocation.CityId);
        Assert.Equal(1.30m, reallocation.SourceOccupancyBefore);
        Assert.Equal(1.00m, reallocation.SourceOccupancyAfter);
        Assert.Equal(0.50m, reallocation.DestinationOccupancyBefore);
        Assert.Equal(0.80m, reallocation.DestinationOccupancyAfter);
        Assert.Contains(scenario.Reasons, reason => reason.Code == "RelievesOverload");
    }

    [Fact]
    public async Task SolveAsync_SuggestsTruckChangeOnlyWhenReallocationIsNotSafe()
    {
        var problem = Problem(
            [
                Route("origem", "AGUDOS NOVA", 10_000m, 13_000m, [City("Agudos", 3_000m, -22.46m, -48.99m), City("Bauru", 10_000m, -22.31m, -49.06m)]),
                Route("destino", "BOCAINA", 10_000m, 9_500m, [City("Bocaina", 9_500m, -22.13m, -48.52m)])
            ],
            [Truck("Truck", 10_000m), Truck("Truck maior", 15_000m)]);

        var solver = new SingleRouteOptimizationSolver(new GeographicDistanceMatrixProvider());

        var solution = await solver.SolveAsync(problem, CancellationToken.None);

        Assert.Equal(RouteOptimizationStatus.Completed, solution.Status);
        var scenario = Assert.Single(solution.Scenarios);
        Assert.Equal(RouteOptimizationActionType.ChangeTruck, scenario.ActionType);
        Assert.NotNull(scenario.TruckChange);
        Assert.Equal("Truck maior", scenario.TruckChange!.ProposedTruckModelName);
        Assert.Equal(13_000m / 15_000m, scenario.TruckChange.OccupancyAfter);
        Assert.Contains(scenario.Warnings, warning => warning.Contains("modelo compatível cadastrado"));
    }

    [Fact]
    public async Task SolveAsync_ReturnsInsufficientDataWhenCityDemandIsMissing()
    {
        var problem = Problem(
            [
                Route("origem", "AGUDOS NOVA", 10_000m, 13_000m, [City("Agudos", -1m, -22.46m, -48.99m)])
            ],
            [Truck("Truck", 10_000m)]);

        var solver = new SingleRouteOptimizationSolver(new GeographicDistanceMatrixProvider());

        var solution = await solver.SolveAsync(problem, CancellationToken.None);

        Assert.Equal(RouteOptimizationStatus.InsufficientData, solution.Status);
        Assert.Equal(RouteOptimizationConfidence.Insufficient, solution.Confidence);
        Assert.Contains(solution.Reasons, reason => reason.Code == "MissingCityDemand");
    }

    [Fact]
    public async Task SolveAsync_IgnoresCitiesWithoutCoordinatesWhenAnotherCityCanBeReallocated()
    {
        var sourceCity = City("Agudos", 3_000m, -22.46m, -48.99m);
        var problem = Problem(
            [
                Route("origem", "AGUDOS NOVA", 10_000m, 13_000m, [sourceCity, CityWithoutCoordinates("Cidade sem coordenada", 10_000m)]),
                Route("destino", "BOCAINA", 10_000m, 5_000m, [City("Bocaina", 5_000m, -22.13m, -48.52m)])
            ],
            [Truck("Truck", 10_000m), Truck("Truck maior", 15_000m)]);

        var solver = new SingleRouteOptimizationSolver(new GeographicDistanceMatrixProvider());

        var solution = await solver.SolveAsync(problem, CancellationToken.None);

        Assert.Equal(RouteOptimizationStatus.Completed, solution.Status);
        var scenario = Assert.Single(solution.Scenarios);
        Assert.Single(scenario.CityReallocations);
        Assert.Contains(scenario.Warnings, warning => warning.Contains("sem coordenadas"));
    }

    [Fact]
    public async Task SolveAsync_DescribesOsrmRoadDistanceWhenProviderUsesRoadRouting()
    {
        var problem = Problem(
            [
                Route("origem", "AGUDOS NOVA", 10_000m, 13_000m, [City("Agudos", 3_000m, -22.46m, -48.99m), City("Bauru", 10_000m, -22.31m, -49.06m)]),
                Route("destino", "BOCAINA", 10_000m, 5_000m, [City("Bocaina", 5_000m, -22.13m, -48.52m)])
            ],
            [Truck("Truck", 10_000m), Truck("Truck maior", 15_000m)]);

        var solver = new SingleRouteOptimizationSolver(new FixedRoadDistanceProvider());

        var solution = await solver.SolveAsync(problem, CancellationToken.None);

        var scenario = Assert.Single(solution.Scenarios);
        Assert.Contains(scenario.Warnings, warning => warning.Contains("OSRM/OpenStreetMap"));
    }

    private static RouteOptimizationProblem Problem(
        IReadOnlyList<OptimizationRoute> routes,
        IReadOnlyList<OptimizationTruckModel> truckModels)
    {
        var source = routes[0];
        return new RouteOptimizationProblem(
            RouteOptimizationScope.SingleRoute,
            new DateOnly(2026, 7, 18),
            source.RouteId,
            Guid.NewGuid(),
            1,
            routes,
            truckModels,
            new OptimizationConstraints(2, 8, 0.05m, 1.00m, 80m),
            "hash");
    }

    private static OptimizationRoute Route(
        string routeIdSeed,
        string name,
        decimal capacityKg,
        decimal loadKg,
        IReadOnlyList<OptimizationCity> cities)
    {
        var routeId = GuidFromSeed(routeIdSeed);
        return new OptimizationRoute(
            routeId,
            name,
            "MONDAY",
            GuidFromSeed("truck-" + routeIdSeed),
            "Truck",
            capacityKg,
            loadKg,
            loadKg / capacityKg,
            cities);
    }

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
