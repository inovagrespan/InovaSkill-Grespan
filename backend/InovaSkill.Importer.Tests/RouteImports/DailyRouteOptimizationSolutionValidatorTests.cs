using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class DailyRouteOptimizationSolutionValidatorTests
{
    [Fact]
    public void Validate_AcceptsExactLoadDistanceAndDurationTotals()
    {
        var problem = Problem();
        var vehicle = problem.ExistingVehicles[0];
        var solution = new RouteOptimizationSolution(DailyRouteOptimizationStatuses.Optimized, null,
        [
            new(vehicle,
                [new(0, 100, 100), new(1, 100, 100)],
                3_000, 400, 400)
        ], 400, 400);

        DailyRouteOptimizationSolutionValidator.Validate(problem, solution);
    }

    [Fact]
    public void Validate_RejectsDuplicatedOrMissingBlock()
    {
        var problem = Problem();
        var vehicle = problem.ExistingVehicles[0];
        var solution = new RouteOptimizationSolution(DailyRouteOptimizationStatuses.Optimized, null,
            [new(vehicle, [new(0, 100, 100), new(0, 0, 0)], 2_000, 200, 200)], 200, 200);

        var error = Assert.Throws<InvalidOperationException>(() =>
            DailyRouteOptimizationSolutionValidator.Validate(problem, solution));

        Assert.Contains("exatamente uma vez", error.Message);
    }

    [Fact]
    public void Validate_RejectsCapacityAndVehicleLoadInconsistency()
    {
        var problem = Problem();
        var vehicle = problem.ExistingVehicles[0] with { CapacityGrams = 2_500 };
        var solution = new RouteOptimizationSolution(DailyRouteOptimizationStatuses.Optimized, null,
            [new(vehicle, [new(0, 100, 100), new(1, 100, 100)], 3_000, 400, 400)], 400, 400);

        var error = Assert.Throws<InvalidOperationException>(() =>
            DailyRouteOptimizationSolutionValidator.Validate(problem, solution));

        Assert.Contains("capacidade", error.Message);
    }

    [Fact]
    public void Validate_RejectsVehiclesForStatusWithoutProposal()
    {
        var problem = Problem();
        var solution = new RouteOptimizationSolution(DailyRouteOptimizationStatuses.NoImprovement, "Sem melhoria",
            [new(problem.ExistingVehicles[0], [], 0, 0, 0)], 0, 0);

        Assert.Throws<InvalidOperationException>(() =>
            DailyRouteOptimizationSolutionValidator.Validate(problem, solution));
    }

    [Fact]
    public void Validate_IncludesServiceTimeAndRejectsRouteAboveMaximum()
    {
        var problem = Problem();
        var vehicle = problem.ExistingVehicles[0];
        var valid = new RouteOptimizationSolution(DailyRouteOptimizationStatuses.Optimized, null,
            [new(vehicle, [new(0, 100, 100), new(1, 100, 100)], 3_000, 400, 2_200)],
            400, 2_200, 900, 2_880);

        DailyRouteOptimizationSolutionValidator.Validate(problem, valid);

        var invalid = valid with { Vehicles = [valid.Vehicles[0] with { DurationSeconds = 2_881 }], TotalDurationSeconds = 2_881 };
        var error = Assert.Throws<InvalidOperationException>(() =>
            DailyRouteOptimizationSolutionValidator.Validate(problem, invalid));
        Assert.Contains("limite de duração", error.Message);
    }

    [Theory]
    [InlineData("Unknown", "Motivo")]
    [InlineData(DailyRouteOptimizationStatuses.Infeasible, null)]
    public void Validate_RejectsUnknownStatusOrMissingDiagnostic(string status, string? reason)
    {
        var solution = new RouteOptimizationSolution(status, reason, [], 0, 0);

        Assert.Throws<InvalidOperationException>(() =>
            DailyRouteOptimizationSolutionValidator.Validate(Problem(), solution));
    }

    private static RouteOptimizationProblem Problem()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var points = new[]
        {
            new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, 0, 0),
            new OsrmMatrixPoint(first, OsrmMatrixPointTypes.Municipality, 0, 0),
            new OsrmMatrixPoint(second, OsrmMatrixPointTypes.Municipality, 0, 0)
        };
        IReadOnlyList<IReadOnlyList<decimal>> values =
        [
            new decimal[] { 0, 100, 200 },
            new decimal[] { 100, 0, 100 },
            new decimal[] { 200, 100, 0 }
        ];
        return new("MONDAY",
            [new(first, "A", 1_000), new(second, "B", 2_000)],
            [new(Guid.NewGuid(), Guid.NewGuid(), "Truck", 4_000, false)],
            [], new("TEST", points, values, values));
    }
}
