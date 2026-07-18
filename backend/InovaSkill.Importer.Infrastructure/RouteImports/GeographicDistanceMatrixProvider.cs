using InovaSkill.Importer.Application.RouteImports;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class GeographicDistanceMatrixProvider : IDistanceMatrixProvider
{
    private const decimal EarthRadiusKm = 6371m;

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

    private static double ToRadians(decimal degrees) => (double)degrees * Math.PI / 180d;
}
