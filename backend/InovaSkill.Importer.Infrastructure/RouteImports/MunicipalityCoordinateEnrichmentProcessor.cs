using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class MunicipalityCoordinateEnrichmentProcessor(
    ImportDbContext dbContext,
    IMunicipalityCoordinateProvider coordinateProvider) : IOperationalJobProcessor
{
    public string JobType => OperationalJobCodes.MunicipalityCoordinateEnrichment;

    public async Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken)
    {
        var municipalityIds = await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == relatedEntityId)
            .Select(snapshot => snapshot.MunicipalityId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (municipalityIds.Count == 0) return;

        var municipalities = await dbContext.Municipalities
            .Include(municipality => municipality.Coordinate)
            .Where(municipality => municipalityIds.Contains(municipality.Id))
            .Where(municipality => municipality.Coordinate == null ||
                municipality.Coordinate.Status != MunicipalityCoordinateStatuses.Resolved ||
                municipality.Coordinate.Latitude == null ||
                municipality.Coordinate.Longitude == null)
            .OrderBy(municipality => municipality.StateCode)
            .ThenBy(municipality => municipality.NormalizedName)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var municipality in municipalities)
        {
            var resolved = await coordinateProvider.ResolveAsync(municipality, cancellationToken);
            var coordinate = municipality.Coordinate ?? new MunicipalityCoordinate
            {
                Id = Guid.NewGuid(),
                MunicipalityId = municipality.Id,
                CreatedAt = now
            };

            coordinate.Source = coordinateProvider.SourceName;
            coordinate.LastAttemptAt = now;
            coordinate.UpdatedAt = now;

            if (resolved is null)
            {
                coordinate.Latitude = null;
                coordinate.Longitude = null;
                coordinate.Status = MunicipalityCoordinateStatuses.Failed;
                coordinate.FailureReason = "Município não encontrado na base estática de coordenadas.";
            }
            else
            {
                municipality.IbgeCode ??= resolved.IbgeCode;
                coordinate.Latitude = resolved.Latitude;
                coordinate.Longitude = resolved.Longitude;
                coordinate.Status = MunicipalityCoordinateStatuses.Resolved;
                coordinate.FailureReason = null;
                coordinate.ResolvedAt = now;
            }

            if (municipality.Coordinate is null)
            {
                dbContext.MunicipalityCoordinates.Add(coordinate);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
