using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class OsrmDailyMatrixService(ImportDbContext db, IOsrmTableClient client)
    : IOsrmDailyMatrixService
{
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
        var entries = await db.RouteEntries.AsNoTracking()
            .Where(entry => entry.Route!.ImportId == routeImportId && entry.Route.Weekday == normalizedWeekday)
            .Select(entry => new
            {
                entry.MunicipalityId,
                MunicipalityName = entry.Municipality != null ? entry.Municipality.Name : entry.Name,
                StateCode = entry.Municipality != null ? entry.Municipality.StateCode : string.Empty,
                Coordinate = entry.Municipality != null ? entry.Municipality.Coordinate : null
            })
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
            throw new OsrmTableException("Não existem cidades para o snapshot e dia informados.");
        if (entries.Any(entry => entry.MunicipalityId is null))
            throw new OsrmTableException("Há cidade da rota sem município identificado.");

        var municipalities = entries
            .GroupBy(entry => entry.MunicipalityId!.Value)
            .Select(group => group.First())
            .OrderBy(entry => entry.StateCode)
            .ThenBy(entry => entry.MunicipalityName)
            .ToArray();
        if (municipalities.Any(entry =>
                entry.Coordinate is null ||
                entry.Coordinate.Status != MunicipalityCoordinateStatuses.Resolved ||
                entry.Coordinate.Latitude is null ||
                entry.Coordinate.Longitude is null))
            throw new OsrmTableException("Há cidade do dia sem coordenada municipal resolvida.");

        var points = new List<OsrmMatrixPoint>(municipalities.Length + 1)
        {
            new(depot.Id, OsrmMatrixPointTypes.Depot, depot.Latitude, depot.Longitude)
        };
        points.AddRange(municipalities.Select(entry => new OsrmMatrixPoint(
            entry.MunicipalityId!.Value,
            OsrmMatrixPointTypes.Municipality,
            entry.Coordinate!.Latitude!.Value,
            entry.Coordinate.Longitude!.Value)));
        return await client.GetTableAsync(new OsrmTableRequest(normalizedWeekday, points), cancellationToken);
    }
}
