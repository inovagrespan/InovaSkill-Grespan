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
    public string SourceCode => RouteImportCodes.DataSource;

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
        var now = DateTime.UtcNow;
        foreach (var parsedRoute in parsed.Routes)
        {
            var route = new Route
            {
                Id = Guid.NewGuid(),
                ImportId = importId,
                Name = parsedRoute.Name,
                Weekday = parsedRoute.Weekday,
                VehicleTypeId = vehicleTypes[parsedRoute.VehicleType].Id,
                CreatedAt = now
            };
            route.Entries = parsedRoute.Entries.Select(entry => new RouteEntry
            {
                Id = Guid.NewGuid(),
                RouteId = route.Id,
                Sequence = entry.Sequence,
                Name = entry.Name,
                Deliveries = entry.Deliveries,
                AveragePerDay = entry.AveragePerDay,
                Note = entry.Note,
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

        foreach (var route in routes)
        {
            if (existing.All(x => !string.Equals(x.Name, route.VehicleType, StringComparison.OrdinalIgnoreCase)))
            {
                var vehicleType = new VehicleType
                {
                    Id = Guid.NewGuid(),
                    Name = route.VehicleType,
                    CapacityKg = route.VehicleCapacityKg
                };
                existing.Add(vehicleType);
                dbContext.VehicleTypes.Add(vehicleType);
            }
        }

        return existing.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }
}
