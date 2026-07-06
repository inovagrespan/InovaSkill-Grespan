using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class RoutesByCityProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    RoutesSpreadsheetParser parser) : IDataSourceProcessor
{
    public string SourceCode => RouteImportCodes.ProcessorKey;

    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
        {
            var lockKey = BitConverter.ToInt64(importId.ToByteArray(), 0);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockKey})",
                cancellationToken);
        }

        var import = await dbContext.RouteImports
            .Include(x => x.Errors)
            .SingleAsync(x => x.Id == importId, cancellationToken);
        var corrections = import.Errors
            .Where(x => x.Status == ImportErrorStatus.Resolved && x.CorrectedValue is not null)
            .Select(x => new SpreadsheetCorrection(x.SheetName, x.RowNumber, x.Field, x.CorrectedValue!))
            .ToArray();

        await using var content = await fileStorage.OpenReadAsync(import.FilePath, cancellationToken);
        var parsed = parser.Parse(content, corrections);

        await dbContext.Routes.Where(x => x.ImportId == importId).ExecuteDeleteAsync(cancellationToken);
        var pendingErrors = import.Errors.Where(x => x.Status == ImportErrorStatus.Pending).ToArray();
        dbContext.RouteImportErrors.RemoveRange(pendingErrors);

        var vehicleTypes = await EnsureVehicleTypesAsync(parsed.Routes, cancellationToken);
        var routeMunicipalityNames = parsed.Routes.SelectMany(route => route.Entries)
            .Select(entry => MunicipalityNameNormalizer.Normalize(entry.Name)).Distinct().ToArray();
        var municipalityCandidates = await dbContext.Municipalities
            .Where(item => routeMunicipalityNames.Contains(item.NormalizedName))
            .ToListAsync(cancellationToken);
        var unambiguousMunicipalities = municipalityCandidates
            .GroupBy(item => item.NormalizedName)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().Id);
        var now = DateTime.UtcNow;
        foreach (var parsedRoute in parsed.Routes)
        {
            var vehicleType = vehicleTypes[parsedRoute.VehicleType];
            var entries = parsedRoute.Entries
                .Select(entry => new
                {
                    Parsed = entry,
                    LoadKg = RouteLoadPolicy.Normalize(entry.AveragePerDay)
                })
                .ToArray();
            var totalWeightKg = entries.Sum(entry => entry.LoadKg);
            var occupancy = RouteOccupancyCalculator.Calculate(new RouteOccupancyInput(
                totalWeightKg,
                vehicleType.CapacityKg,
                null,
                vehicleType.CapacityVolumeM3,
                null,
                vehicleType.CapacityPallets));
            var route = new Route
            {
                Id = Guid.NewGuid(),
                ImportId = importId,
                Name = parsedRoute.Name,
                Weekday = parsedRoute.Weekday,
                VehicleTypeId = vehicleType.Id,
                TotalWeightKg = totalWeightKg,
                WeightOccupancy = occupancy.WeightOccupancy,
                VolumeOccupancy = occupancy.VolumeOccupancy,
                PalletOccupancy = occupancy.PalletOccupancy,
                OverallOccupancy = occupancy.OverallOccupancy,
                OccupancyStatus = occupancy.HasAvailableCapacity
                    ? RouteOccupancyStatus.Calculated
                    : RouteOccupancyStatus.MissingCapacity,
                CreatedAt = now
            };
            route.Entries = entries.Select(entry => new RouteEntry
            {
                Id = Guid.NewGuid(),
                RouteId = route.Id,
                Sequence = entry.Parsed.Sequence,
                Name = entry.Parsed.Name,
                MunicipalityId = unambiguousMunicipalities.TryGetValue(
                    MunicipalityNameNormalizer.Normalize(entry.Parsed.Name), out var municipalityId)
                    ? municipalityId
                    : null,
                Deliveries = entry.Parsed.Deliveries,
                AveragePerDay = entry.LoadKg,
                Note = entry.Parsed.Note,
                CreatedAt = now
            }).ToArray();
            dbContext.Routes.Add(route);
        }

        foreach (var error in parsed.Errors)
        {
            dbContext.RouteImportErrors.Add(new RouteImportError
            {
                Id = Guid.NewGuid(),
                ImportId = importId,
                SheetName = error.SheetName,
                RowNumber = error.RowNumber,
                Field = error.Field,
                RawValue = error.RawValue,
                Message = error.Message,
                Status = ImportErrorStatus.Pending,
                CreatedAt = now
            });
        }

        import.TotalRows = parsed.TotalRows;
        import.ImportedRows = parsed.ImportedRows;
        import.ErrorCount = parsed.Errors.Count;
        import.Status = parsed.Errors.Count == 0 ? RouteImportStatus.Completed : RouteImportStatus.NeedsReview;
        import.FinishedAt = now;
        import.FailureMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<Dictionary<string, VehicleType>> EnsureVehicleTypesAsync(
        IReadOnlyList<ParsedRoute> routes,
        CancellationToken cancellationToken)
    {
        var names = routes.Select(x => x.VehicleType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = await dbContext.VehicleTypes
            .Where(x => names.Contains(x.Name))
            .ToListAsync(cancellationToken);

        foreach (var name in names)
        {
            var existingVehicleType = existing.SingleOrDefault(
                item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            var parsedCapacity = routes
                .Where(route => string.Equals(route.VehicleType, name, StringComparison.OrdinalIgnoreCase))
                .Select(route => route.VehicleCapacityKg)
                .FirstOrDefault(capacity => capacity > 0);
            var knownCapacity = parsedCapacity > 0
                ? parsedCapacity
                : LogisticsVehicleCapacityPolicy.FindWeightCapacityKg(name);

            if (existingVehicleType is null)
            {
                var vehicleType = new VehicleType
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    CapacityKg = knownCapacity
                };
                existing.Add(vehicleType);
                dbContext.VehicleTypes.Add(vehicleType);
            }
            else if (existingVehicleType.CapacityKg is null && knownCapacity.HasValue)
            {
                existingVehicleType.CapacityKg = knownCapacity;
            }
        }

        return existing.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }
}
