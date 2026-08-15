using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class DeterministicRouteStopSequenceOptimizer(IRouteTravelMatrixProvider matrixProvider)
    : IRouteStopSequenceOptimizer
{
    private const int StartStopIndex = 0;
    private const int MaximumExactStops = 14;

    public async Task<RouteStopSequenceResult> OptimizeAsync(
        IReadOnlyList<OptimizationCity> stops,
        CancellationToken cancellationToken)
    {
        if (stops.Any(stop => stop.Location is null))
        {
            throw new ArgumentException("Todas as paradas precisam ter coordenadas para otimizar a sequência.", nameof(stops));
        }

        if (stops.Count <= 1)
        {
            return new RouteStopSequenceResult(stops, 0m, 0m, 0, 0, matrixProvider.Method);
        }

        var matrix = await matrixProvider.GetMatrixAsync(
            stops.Select(stop => stop.Location!).ToArray(),
            cancellationToken);
        ValidateMatrix(matrix, stops.Count);

        var currentIndexes = Enumerable.Range(0, stops.Count).ToArray();
        var proposedIndexes = stops.Count <= MaximumExactStops
            ? SolveExactOpenPath(matrix.DurationsMinutes, cancellationToken)
            : ImproveWithTwoOpt(
                BuildNearestNeighborPath(matrix.DurationsMinutes, cancellationToken),
                matrix.DurationsMinutes,
                cancellationToken);

        return new RouteStopSequenceResult(
            proposedIndexes.Select(stopIndex => stops[stopIndex]).ToArray(),
            SumPath(matrix.DistancesKm, currentIndexes),
            SumPath(matrix.DistancesKm, proposedIndexes),
            SumPath(matrix.DurationsMinutes, currentIndexes),
            SumPath(matrix.DurationsMinutes, proposedIndexes),
            matrix.Method);
    }

    private static IReadOnlyList<int> SolveExactOpenPath(
        IReadOnlyList<IReadOnlyList<int>> costs,
        CancellationToken cancellationToken)
    {
        var movableStopCount = costs.Count - 1;
        var stateCount = 1 << movableStopCount;
        var bestCosts = new long[stateCount, movableStopCount];
        var predecessors = new int[stateCount, movableStopCount];
        for (var mask = 0; mask < stateCount; mask++)
        {
            for (var last = 0; last < movableStopCount; last++)
            {
                bestCosts[mask, last] = long.MaxValue;
                predecessors[mask, last] = -1;
            }
        }

        for (var stop = 0; stop < movableStopCount; stop++)
        {
            var mask = 1 << stop;
            bestCosts[mask, stop] = costs[StartStopIndex][stop + 1];
        }

        for (var mask = 1; mask < stateCount; mask++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var last = 0; last < movableStopCount; last++)
            {
                var currentCost = bestCosts[mask, last];
                if (currentCost == long.MaxValue || (mask & (1 << last)) == 0) continue;

                for (var next = 0; next < movableStopCount; next++)
                {
                    if ((mask & (1 << next)) != 0) continue;
                    var nextMask = mask | (1 << next);
                    var candidateCost = currentCost + costs[last + 1][next + 1];
                    if (candidateCost < bestCosts[nextMask, next])
                    {
                        bestCosts[nextMask, next] = candidateCost;
                        predecessors[nextMask, next] = last;
                    }
                }
            }
        }

        var fullMask = stateCount - 1;
        var end = Enumerable.Range(0, movableStopCount)
            .OrderBy(stop => bestCosts[fullMask, stop])
            .ThenBy(stop => stop)
            .First();
        var reversed = new List<int>(movableStopCount);
        var currentMask = fullMask;
        while (end >= 0)
        {
            reversed.Add(end + 1);
            var predecessor = predecessors[currentMask, end];
            currentMask &= ~(1 << end);
            end = predecessor;
        }

        reversed.Reverse();
        return new[] { StartStopIndex }.Concat(reversed).ToArray();
    }

    private static IReadOnlyList<int> BuildNearestNeighborPath(
        IReadOnlyList<IReadOnlyList<int>> costs,
        CancellationToken cancellationToken)
    {
        var remaining = new SortedSet<int>(Enumerable.Range(1, costs.Count - 1));
        var path = new List<int>(costs.Count) { StartStopIndex };
        while (remaining.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = path[^1];
            var next = remaining.OrderBy(candidate => costs[current][candidate]).ThenBy(candidate => candidate).First();
            path.Add(next);
            remaining.Remove(next);
        }

        return path;
    }

    private static IReadOnlyList<int> ImproveWithTwoOpt(
        IReadOnlyList<int> initialPath,
        IReadOnlyList<IReadOnlyList<int>> costs,
        CancellationToken cancellationToken)
    {
        var path = initialPath.ToArray();
        var improved = true;
        while (improved)
        {
            improved = false;
            var currentCost = SumPath(costs, path);
            for (var start = 1; start < path.Length - 1 && !improved; start++)
            {
                for (var end = start + 1; end < path.Length; end++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidate = path.ToArray();
                    Array.Reverse(candidate, start, end - start + 1);
                    if (SumPath(costs, candidate) >= currentCost) continue;
                    path = candidate;
                    improved = true;
                    break;
                }
            }
        }

        return path;
    }

    private static decimal SumPath(
        IReadOnlyList<IReadOnlyList<decimal>> matrix,
        IReadOnlyList<int> indexes) =>
        indexes.Zip(indexes.Skip(1), (origin, destination) => matrix[origin][destination]).Sum();

    private static int SumPath(
        IReadOnlyList<IReadOnlyList<int>> matrix,
        IReadOnlyList<int> indexes) =>
        indexes.Zip(indexes.Skip(1), (origin, destination) => matrix[origin][destination]).Sum();

    private static void ValidateMatrix(RouteTravelMatrix matrix, int expectedSize)
    {
        if (matrix.DistancesKm.Count != expectedSize ||
            matrix.DurationsMinutes.Count != expectedSize ||
            matrix.DistancesKm.Any(row => row.Count != expectedSize) ||
            matrix.DurationsMinutes.Any(row => row.Count != expectedSize))
        {
            throw new InvalidOperationException("O provedor retornou uma matriz de viagem com dimensão inválida.");
        }
    }
}
