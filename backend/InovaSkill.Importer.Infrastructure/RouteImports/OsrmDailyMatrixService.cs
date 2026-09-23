using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OsrmDailyMatrixService(
    ImportDbContext db,
    IOsrmTableClient client,
    IOptions<OsrmOptions>? options = null)
    : IOsrmDailyMatrixService
{
    private const double EarthRadiusMeters = 6_371_000d;
    private const decimal MinimumDistinctLegDistanceMeters = 1m;
    private const decimal MinimumDistinctLegDurationSeconds = 1m;
    private readonly decimal fallbackSpeedKph = Math.Max(1m, options?.Value.MatrixFallbackSpeedKph ?? 50m);

    public async Task<OsrmTableResult> GetForDayAsync(
        Guid routeImportId,
        string weekday,
        CancellationToken cancellationToken)
    {
        var normalizedWeekday = weekday?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedWeekday))
            throw new ArgumentException("O dia da semana é obrigatório.", nameof(weekday));

        var depot = await db.LogisticsDepots.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new OsrmTableException("O depósito logístico não foi configurado.");
        var customers = await db.RouteCustomerAssignments.AsNoTracking()
            .Where(assignment => assignment.Route!.ImportId == routeImportId &&
                assignment.Route.Weekday == normalizedWeekday &&
                assignment.Route.Entries.Any(entry => !entry.IsExcludedFromOptimization &&
                    entry.AveragePerDay > 0 &&
                    entry.MunicipalityId == assignment.MunicipalityId))
            .Select(assignment => new
            {
                assignment.CustomerId,
                AddressCity = assignment.Customer!.RegistrationAddress!.City,
                AddressCoordinate = assignment.Customer.RegistrationAddress.Coordinate,
                MunicipalityName = assignment.Municipality!.NormalizedName,
                MunicipalityCoordinate = assignment.Municipality.Coordinate
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        if (customers.Count == 0)
            throw new OsrmTableException("Não existem clientes vinculados às paradas do dia informado.");

        var distinctCustomers = customers
            .GroupBy(item => item.CustomerId)
            .Select(group => group.First())
            .OrderBy(item => item.CustomerId)
            .ToArray();
        var customerCoordinates = distinctCustomers.Select(item => new
        {
            item.CustomerId,
            Coordinate = SelectCoordinate(item.AddressCity, item.AddressCoordinate,
                item.MunicipalityName, item.MunicipalityCoordinate)
        }).ToArray();
        if (customerCoordinates.Any(item => item.Coordinate is null))
            throw new OsrmTableException("Há cliente do dia sem coordenada exata resolvida.");

        var points = new List<OsrmMatrixPoint>(distinctCustomers.Length + 1)
        {
            new(depot.Id, OsrmMatrixPointTypes.Depot, depot.Latitude, depot.Longitude)
        };
        points.AddRange(customerCoordinates.Select(item => new OsrmMatrixPoint(
            item.CustomerId,
            OsrmMatrixPointTypes.Customer,
            item.Coordinate!.Latitude,
            item.Coordinate.Longitude)));
        var matrix = await client.GetTableAsync(new OsrmTableRequest(normalizedWeekday, points), cancellationToken);
        return RepairZeroLegs(matrix);
    }

    private static CoordinateValue? SelectCoordinate(
        string? addressCity,
        CustomerAddressCoordinate? addressCoordinate,
        string? municipalityName,
        MunicipalityCoordinate? municipalityCoordinate)
    {
        if (!CustomerAddressCoordinateQuality.IsExact(addressCoordinate) ||
            addressCoordinate?.Latitude is not decimal addressLatitude ||
            addressCoordinate.Longitude is not decimal addressLongitude)
            return null;
        addressCity = NormalizeCity(addressCity);
        municipalityName = NormalizeCity(municipalityName);
        if (string.IsNullOrWhiteSpace(addressCity) || string.IsNullOrWhiteSpace(municipalityName) ||
            string.Equals(addressCity, municipalityName, StringComparison.Ordinal))
            return new(addressLatitude, addressLongitude);

        return municipalityCoordinate is
        {
            Status: MunicipalityCoordinateStatuses.Resolved,
            Latitude: not null,
            Longitude: not null
        }
            ? new(municipalityCoordinate.Latitude.Value, municipalityCoordinate.Longitude.Value)
            : new(addressLatitude, addressLongitude);
    }

    private static string NormalizeCity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(character))
                builder.Append(character);
        }
        return builder.ToString();
    }

    private sealed record CoordinateValue(decimal Latitude, decimal Longitude);

    private OsrmTableResult RepairZeroLegs(OsrmTableResult matrix)
    {
        var distances = matrix.DistancesMeters.Select(row => row.ToArray()).ToArray();
        var durations = matrix.DurationsSeconds.Select(row => row.ToArray()).ToArray();
        for (var source = 0; source < matrix.Points.Count; source++)
        for (var destination = source + 1; destination < matrix.Points.Count; destination++)
        {
            var sourcePoint = matrix.Points[source];
            var destinationPoint = matrix.Points[destination];
            if (sourcePoint.Latitude == destinationPoint.Latitude &&
                sourcePoint.Longitude == destinationPoint.Longitude)
                continue;
            var geographicDistance = HaversineMeters(sourcePoint, destinationPoint);
            var geographicDuration = geographicDistance / (double)fallbackSpeedKph * 3.6d;
            distances[source][destination] = PositiveDistinctLegMetric(
                distances[source][destination], geographicDistance, MinimumDistinctLegDistanceMeters);
            distances[destination][source] = PositiveDistinctLegMetric(
                distances[destination][source], geographicDistance, MinimumDistinctLegDistanceMeters);
            durations[source][destination] = PositiveDistinctLegMetric(
                durations[source][destination], geographicDuration, MinimumDistinctLegDurationSeconds);
            durations[destination][source] = PositiveDistinctLegMetric(
                durations[destination][source], geographicDuration, MinimumDistinctLegDurationSeconds);
        }
        return new OsrmTableResult(matrix.Source, matrix.Points,
            durations.Select(row => (IReadOnlyList<decimal>)row).ToArray(),
            distances.Select(row => (IReadOnlyList<decimal>)row).ToArray());
    }

    private static double HaversineMeters(OsrmMatrixPoint first, OsrmMatrixPoint second)
    {
        var latitudeDelta = DegreesToRadians((double)(second.Latitude - first.Latitude));
        var longitudeDelta = DegreesToRadians((double)(second.Longitude - first.Longitude));
        var latitude = DegreesToRadians((double)first.Latitude);
        var secondLatitude = DegreesToRadians((double)second.Latitude);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
            Math.Cos(latitude) * Math.Cos(secondLatitude) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return EarthRadiusMeters * 2 * Math.Asin(Math.Sqrt(a));
    }

    private static decimal PositiveDistinctLegMetric(decimal value, double geographicFallback, decimal minimum)
    {
        if (value >= minimum) return value;
        return Math.Max((decimal)geographicFallback, minimum);
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
}
