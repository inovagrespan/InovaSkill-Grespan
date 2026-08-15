using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class GeographicDistanceMatrixProvider : IDistanceMatrixProvider, IRouteTravelMatrixProvider
{
    private const decimal EarthRadiusKm = 6371m;
    private const decimal EstimatedRoadFactor = 1.25m;
    private const decimal EstimatedAverageSpeedKmPerHour = 60m;
    private const int MinutesPerHour = 60;

    public string Method => "GeographicCoordinates";

    public Task<decimal> GetDistanceKmAsync(
        GeoPoint origin,
        GeoPoint destination,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var originLatitude = ToRadians(origin.Latitude);
        var destinationLatitude = ToRadians(destination.Latitude);
        var latitudeDelta = ToRadians(destination.Latitude - origin.Latitude);
        var longitudeDelta = ToRadians(destination.Longitude - origin.Longitude);

        var a = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d) +
                Math.Cos(originLatitude) * Math.Cos(destinationLatitude) *
                Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        var distance = EarthRadiusKm * (decimal)c;

        return Task.FromResult(Math.Round(distance, 2, MidpointRounding.AwayFromZero));
    }

    public async Task<RouteTravelMatrix> GetMatrixAsync(
        IReadOnlyList<GeoPoint> points,
        CancellationToken cancellationToken)
    {
        var distances = new List<IReadOnlyList<decimal>>(points.Count);
        var durations = new List<IReadOnlyList<int>>(points.Count);
        for (var originIndex = 0; originIndex < points.Count; originIndex++)
        {
            var distanceRow = new decimal[points.Count];
            var durationRow = new int[points.Count];
            for (var destinationIndex = 0; destinationIndex < points.Count; destinationIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var geographicDistance = await GetDistanceKmAsync(
                    points[originIndex],
                    points[destinationIndex],
                    cancellationToken);
                var roadDistance = Math.Round(
                    geographicDistance * EstimatedRoadFactor,
                    2,
                    MidpointRounding.AwayFromZero);
                distanceRow[destinationIndex] = roadDistance;
                durationRow[destinationIndex] = (int)Math.Round(
                    roadDistance / EstimatedAverageSpeedKmPerHour * MinutesPerHour,
                    MidpointRounding.AwayFromZero);
            }

            distances.Add(distanceRow);
            durations.Add(durationRow);
        }

        return new RouteTravelMatrix(distances, durations, Method);
    }

    private static double ToRadians(decimal degrees) => (double)degrees * Math.PI / 180d;
}
