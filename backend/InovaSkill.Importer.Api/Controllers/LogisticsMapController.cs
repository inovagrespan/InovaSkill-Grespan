using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/logistics/map")]
public sealed class LogisticsMapController(ImportDbContext dbContext) : ControllerBase
{
    private const decimal CustomerPinBaseRadiusDegrees = 0.006m;
    private const decimal CustomerPinRadiusStepDegrees = 0.0015m;
    private const int CustomerPinRadiusStepCount = 4;

    [HttpGet("customers")]
    public async Task<ActionResult> Customers(
        [FromQuery] string? active = null,
        CancellationToken cancellationToken = default)
    {
        var currentImportId = await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == CustomerImportCodes.DataSource)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!currentImportId.HasValue)
        {
            return Ok(new { total = 0, visible = 0, withoutCoordinates = 0, items = Array.Empty<object>() });
        }

        var query = dbContext.CustomerSnapshots.AsNoTracking()
            .Include(snapshot => snapshot.Customer)
            .Include(snapshot => snapshot.Municipality)
            .Where(snapshot => snapshot.ImportId == currentImportId.Value);

        if (string.Equals(active, "true", StringComparison.OrdinalIgnoreCase))
            query = query.Where(s => s.Customer!.IsActive);
        else if (string.Equals(active, "false", StringComparison.OrdinalIgnoreCase))
            query = query.Where(s => !s.Customer!.IsActive);

        var snapshots = await query
            .OrderBy(snapshot => snapshot.Municipality!.Name)
            .ThenBy(snapshot => snapshot.Customer!.ExternalCode)
            .ThenBy(snapshot => snapshot.Customer!.BranchCode)
            .ToListAsync(cancellationToken);
        var municipalityIds = snapshots.Select(snapshot => snapshot.MunicipalityId)
            .Distinct()
            .ToArray();
        var coordinates = await dbContext.MunicipalityCoordinates.AsNoTracking()
            .Where(item => municipalityIds.Contains(item.MunicipalityId) &&
                item.Status == MunicipalityCoordinateStatuses.Resolved)
            .ToDictionaryAsync(item => item.MunicipalityId, cancellationToken);
        var customerIds = snapshots.Select(snapshot => snapshot.CustomerId).ToArray();
        var addressCoordinates = await dbContext.CustomerAddressCoordinates.AsNoTracking()
            .Where(item => customerIds.Contains(item.CustomerRegistrationAddress!.CustomerId) &&
                item.Status == CustomerAddressCoordinateStatuses.Resolved &&
                item.Latitude != null && item.Longitude != null)
            .Select(item => new
            {
                item.CustomerRegistrationAddress!.CustomerId,
                item.Latitude,
                item.Longitude,
                item.Precision,
                item.CustomerRegistrationAddress.StreetType,
                item.CustomerRegistrationAddress.Street,
                item.CustomerRegistrationAddress.Number,
                item.CustomerRegistrationAddress.Neighborhood,
                item.CustomerRegistrationAddress.City,
                item.CustomerRegistrationAddress.StateCode,
                item.CustomerRegistrationAddress.PostalCode
            })
            .ToDictionaryAsync(item => item.CustomerId, cancellationToken);
        var rows = snapshots.Select(snapshot =>
            {
                coordinates.TryGetValue(snapshot.MunicipalityId, out var coordinate);
                addressCoordinates.TryGetValue(snapshot.CustomerId, out var addressCoordinate);
                return new CustomerMapRow(
                    snapshot.CustomerId,
                    snapshot.Customer!.ExternalCode,
                    snapshot.Customer.BranchCode,
                    snapshot.TradeName,
                    snapshot.LegalName,
                    snapshot.CustomerType,
                    snapshot.Customer.IsActive,
                    snapshot.MunicipalityId,
                    snapshot.Municipality!.Name,
                    snapshot.Municipality.StateCode,
                    addressCoordinate?.Latitude ?? coordinate?.Latitude,
                    addressCoordinate?.Longitude ?? coordinate?.Longitude,
                    addressCoordinate is null ? "MUNICIPALITY" :
                        addressCoordinate.Precision == CustomerAddressCoordinatePrecisions.Interpolated
                            ? "ADDRESS_INTERPOLATED" : "ADDRESS_EXACT",
                    addressCoordinate is null ? null : FormatAddress(
                        addressCoordinate.StreetType, addressCoordinate.Street, addressCoordinate.Number, addressCoordinate.Neighborhood,
                        addressCoordinate.City, addressCoordinate.StateCode, addressCoordinate.PostalCode),
                    coordinate?.Latitude,
                    coordinate?.Longitude);
            })
            .OrderBy(item => item.City)
            .ThenBy(item => item.ExternalCode)
            .ThenBy(item => item.BranchCode)
            .ToList();

        var visibleRows = rows
            .Where(item => item.LocationPrecision == "ADDRESS_EXACT" &&
                item.Latitude.HasValue && item.Longitude.HasValue)
            .GroupBy(item => item.MunicipalityId)
            .SelectMany(group =>
            {
                var groupedRows = group.ToArray();
                return groupedRows.Select((item, index) => ToMapPoint(item, index, groupedRows.Length));
            })
            .ToArray();

        return Ok(new
        {
            total = rows.Count,
            visible = visibleRows.Length,
            withoutCoordinates = rows.Count - visibleRows.Length,
            items = visibleRows
        });
    }

    private static object ToMapPoint(CustomerMapRow row, int index, int municipalityCustomerCount)
    {
        var angle = 2 * Math.PI * index / Math.Max(1, municipalityCustomerCount);
        var radius = CustomerPinBaseRadiusDegrees +
            (index % CustomerPinRadiusStepCount) * CustomerPinRadiusStepDegrees;
        if (municipalityCustomerCount == 1) radius = 0;
        return new
        {
            id = row.CustomerId,
            name = string.IsNullOrWhiteSpace(row.TradeName) ? row.LegalName : row.TradeName,
            isActive = row.IsActive,
            row.ExternalCode,
            row.BranchCode,
            city = row.City,
            row.StateCode,
            type = string.IsNullOrWhiteSpace(row.CustomerType) ? "Não informado" : row.CustomerType,
            status = "Normal",
            situation = "Entrega normal",
            priority = "Baixa",
            route = $"Município: {row.City}/{row.StateCode}",
            lastDelivery = "Não disponível",
            nextDelivery = "Não disponível",
            locationPrecision = row.LocationPrecision,
            address = row.Address,
            lat = row.LocationPrecision != "MUNICIPALITY" ? Math.Round((double)row.Latitude!.Value, 6) :
                Math.Round((double)row.Latitude!.Value + Math.Cos(angle) * (double)radius, 6),
            lng = row.LocationPrecision != "MUNICIPALITY" ? Math.Round((double)row.Longitude!.Value, 6) :
                Math.Round((double)row.Longitude!.Value + Math.Sin(angle) * (double)radius, 6),
            municipalityLat = row.MunicipalityLatitude,
            municipalityLng = row.MunicipalityLongitude
        };
    }

    private static string FormatAddress(string? streetType, string? street, string? number, string? neighborhood,
        string? city, string? stateCode, string? postalCode)
    {
        var formattedStreet = string.IsNullOrWhiteSpace(street) ? null :
            NominatimAddressCoordinateProvider.FormatStreet(streetType, street);
        var streetLine = string.Join(", ", new[] { formattedStreet, number }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var cityLine = string.Join("/", new[] { city, stateCode }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.Join(" - ", new[] { streetLine, neighborhood, cityLine,
            string.IsNullOrWhiteSpace(postalCode) ? null : $"CEP {postalCode}" }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private sealed record CustomerMapRow(
        Guid CustomerId,
        string ExternalCode,
        string BranchCode,
        string TradeName,
        string LegalName,
        string CustomerType,
        bool IsActive,
        Guid MunicipalityId,
        string City,
        string StateCode,
        decimal? Latitude,
        decimal? Longitude,
        string LocationPrecision,
        string? Address,
        decimal? MunicipalityLatitude,
        decimal? MunicipalityLongitude);
}
