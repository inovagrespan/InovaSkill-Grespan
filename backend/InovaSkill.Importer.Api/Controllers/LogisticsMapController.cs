using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
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
        var rows = snapshots.Select(snapshot =>
            {
                coordinates.TryGetValue(snapshot.MunicipalityId, out var coordinate);
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
                    coordinate?.Latitude,
                    coordinate?.Longitude);
            })
            .OrderBy(item => item.City)
            .ThenBy(item => item.ExternalCode)
            .ThenBy(item => item.BranchCode)
            .ToList();

        var visibleRows = rows
            .Where(item => item.Latitude.HasValue && item.Longitude.HasValue)
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
            locationPrecision = "Municipality",
            lat = Math.Round((double)row.Latitude!.Value + Math.Cos(angle) * (double)radius, 6),
            lng = Math.Round((double)row.Longitude!.Value + Math.Sin(angle) * (double)radius, 6),
            municipalityLat = row.Latitude,
            municipalityLng = row.Longitude
        };
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
        decimal? Longitude);
}
