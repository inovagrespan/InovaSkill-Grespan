using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.RouteImports;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class DeterministicRouteStopSequenceOptimizerTests
{
    [Fact]
    public async Task OptimizeAsync_KeepsFirstStopAndReducesOpenRouteCost()
    {
        var stops = new[]
        {
            Stop("Origem", 1),
            Stop("Distante", 2),
            Stop("Próxima", 3)
        };
        var optimizer = new DeterministicRouteStopSequenceOptimizer(new FixedMatrixProvider());

        var result = await optimizer.OptimizeAsync(stops, CancellationToken.None);

        Assert.Equal(new[] { "Origem", "Próxima", "Distante" }, result.Stops.Select(stop => stop.Name));
        Assert.Equal(11m, result.CurrentDistanceKm);
        Assert.Equal(3m, result.ProposedDistanceKm);
        Assert.Equal(22, result.CurrentDurationMinutes);
        Assert.Equal(6, result.ProposedDurationMinutes);
        Assert.Equal("FixedRoadMatrix", result.MatrixMethod);
    }

    [Fact]
    public async Task OptimizeAsync_RejectsStopWithoutCoordinates()
    {
        var optimizer = new DeterministicRouteStopSequenceOptimizer(new FixedMatrixProvider());
        var stops = new[] { Stop("Origem", 1), Stop("Sem coordenada", 2) with { Location = null } };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            optimizer.OptimizeAsync(stops, CancellationToken.None));

        Assert.Contains("coordenadas", exception.Message);
    }

    private static OptimizationCity Stop(string name, int sequence) =>
        new(Guid.NewGuid(), name, 100m, new GeoPoint(-22m + sequence, -49m), sequence);

    private sealed class FixedMatrixProvider : IRouteTravelMatrixProvider
    {
        public string Method => "FixedRoadMatrix";

        public Task<RouteTravelMatrix> GetMatrixAsync(
            IReadOnlyList<GeoPoint> points,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RouteTravelMatrix(
                new decimal[][]
                {
                    [0m, 10m, 2m],
                    [10m, 0m, 1m],
                    [2m, 1m, 0m]
                },
                new int[][]
                {
                    [0, 20, 4],
                    [20, 0, 2],
                    [4, 2, 0]
                },
                Method));
    }
}
