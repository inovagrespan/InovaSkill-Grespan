using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class RoutesByCityProcessor(
    ImportDbContext dbContext,
    IImportFileStorage fileStorage,
    RoutesSpreadsheetParser parser,
    IMunicipalityCoordinateProvider? municipalityProvider = null) : IDataSourceProcessor
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
            .Include(x => x.Corrections)
            .Include(x => x.AffectedWeekdays)
            .SingleAsync(x => x.Id == importId, cancellationToken);
        if (import.DerivedFromImportId.HasValue)
        {
            await ProcessDerivedSnapshot(import, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }
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
            .Select(entry => RouteMunicipalityAliasPolicy.Resolve(MunicipalityNameNormalizer.Normalize(entry.Name))).Distinct().ToArray();
        var municipalityCandidates = await dbContext.Municipalities
            .Where(item => routeMunicipalityNames.Contains(item.NormalizedName))
            .ToListAsync(cancellationToken);
        var unambiguousMunicipalities = municipalityCandidates
            .GroupBy(item => item.NormalizedName)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().Id);
        var aliases = await dbContext.MunicipalityAliases.AsNoTracking()
            .Where(item => item.DataSourceId == import.DataSourceId && routeMunicipalityNames.Contains(item.NormalizedAlias))
            .ToDictionaryAsync(item => item.NormalizedAlias, item => item.MunicipalityId, cancellationToken);
        foreach (var pair in aliases) unambiguousMunicipalities[pair.Key] = pair.Value;
        if (municipalityProvider is not null)
        {
            foreach (var normalizedName in routeMunicipalityNames.Where(name => !unambiguousMunicipalities.ContainsKey(name)))
            {
                var lookup = await municipalityProvider.ResolveUniqueNameAsync(normalizedName, cancellationToken);
                if (lookup is null) continue;
                var municipality = await dbContext.Municipalities.Include(item => item.Coordinate)
                    .SingleOrDefaultAsync(item => item.StateCode == lookup.StateCode && item.NormalizedName == lookup.NormalizedName,
                        cancellationToken);
                if (municipality is null)
                {
                    municipality = CreateOfficialMunicipality(lookup, municipalityProvider.SourceName, now: DateTime.UtcNow);
                    dbContext.Municipalities.Add(municipality);
                }
                unambiguousMunicipalities[normalizedName] = municipality.Id;
            }
        }
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
                SourceSheetName = parsedRoute.SourceSheetName,
                SourceHeaderRowNumber = parsedRoute.SourceHeaderRowNumber,
                VehicleTypeId = vehicleType.Id,
                VehicleCapacityKgSnapshot = vehicleType.CapacityKg ?? 0,
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
                SourceRowNumber = entry.Parsed.SourceRowNumber,
                Name = entry.Parsed.Name,
                MunicipalityId = unambiguousMunicipalities.TryGetValue(
                    RouteMunicipalityAliasPolicy.Resolve(MunicipalityNameNormalizer.Normalize(entry.Parsed.Name)), out var municipalityId)
                    ? municipalityId
                    : null,
                Deliveries = entry.Parsed.Deliveries,
                AveragePerDay = entry.LoadKg,
                IsExcludedFromOptimization = false,
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

    private async Task ProcessDerivedSnapshot(RouteImport import, CancellationToken cancellationToken)
    {
        var parentId = import.DerivedFromImportId!.Value;
        if (dbContext.Database.IsNpgsql())
        {
            var sourceLockKey = BitConverter.ToInt64(import.DataSourceId.ToByteArray(), 0);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({sourceLockKey})", cancellationToken);
        }

        var source = await dbContext.DataSources.SingleAsync(item => item.Id == import.DataSourceId, cancellationToken);
        if (source.CurrentImportId != parentId)
            throw new StructuralImportException("O snapshot foi atualizado por outro usuário. Reabra as pendências e tente novamente.");

        await dbContext.DailyRouteOptimizationResults.Where(item => item.RouteImportId == import.Id)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.Routes.Where(item => item.ImportId == import.Id).ExecuteDeleteAsync(cancellationToken);

        var parent = await dbContext.RouteImports.AsNoTracking()
            .SingleAsync(item => item.Id == parentId && item.Status == RouteImportStatus.Completed, cancellationToken);
        var parentRoutes = await dbContext.Routes.AsNoTracking()
            .Where(route => route.ImportId == parentId)
            .Include(route => route.VehicleType)
            .Include(route => route.Entries)
            .OrderBy(route => route.Weekday).ThenBy(route => route.Name)
            .ToListAsync(cancellationToken);
        var corrections = import.Corrections.ToArray();
        var now = DateTime.UtcNow;

        await ApplyCatalogCorrections(import, corrections, now, cancellationToken);
        var vehicleTypes = await dbContext.VehicleTypes.ToDictionaryAsync(item => item.Id, cancellationToken);
        var linkCorrections = corrections.Where(item => item.Kind == RouteImportCorrectionKinds.LinkMunicipality)
            .Where(item => item.MunicipalityId.HasValue && !string.IsNullOrWhiteSpace(item.SourceLabel))
            .GroupBy(item => MunicipalityNameNormalizer.Normalize(item.SourceLabel!))
            .ToDictionary(group => group.Key, group => group.Last().MunicipalityId!.Value);
        var entryCorrections = corrections.Where(item => item.SourceRouteEntryId.HasValue)
            .GroupBy(item => item.SourceRouteEntryId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var routeMap = new Dictionary<Guid, Guid>();
        var entryMap = new Dictionary<Guid, Guid>();

        foreach (var parentRoute in parentRoutes)
        {
            var routeId = Guid.NewGuid();
            routeMap[parentRoute.Id] = routeId;
            var capacity = vehicleTypes[parentRoute.VehicleTypeId].CapacityKg ?? 0;
            var route = new Route
            {
                Id = routeId, ImportId = import.Id, Name = parentRoute.Name, Weekday = parentRoute.Weekday,
                SourceSheetName = parentRoute.SourceSheetName, SourceHeaderRowNumber = parentRoute.SourceHeaderRowNumber,
                VehicleTypeId = parentRoute.VehicleTypeId, VehicleType = vehicleTypes[parentRoute.VehicleTypeId],
                VehicleCapacityKgSnapshot = capacity,
                TotalVolumeM3 = parentRoute.TotalVolumeM3, TotalPallets = parentRoute.TotalPallets, CreatedAt = now
            };
            foreach (var parentEntry in parentRoute.Entries.OrderBy(item => item.Sequence))
            {
                var entryId = Guid.NewGuid();
                entryMap[parentEntry.Id] = entryId;
                var normalizedEntryName = MunicipalityNameNormalizer.Normalize(parentEntry.Name);
                var municipalityId = linkCorrections.TryGetValue(normalizedEntryName, out var linkedMunicipalityId)
                    ? linkedMunicipalityId
                    : parentEntry.MunicipalityId;
                var entry = new RouteEntry
                {
                    Id = entryId, RouteId = routeId, Sequence = parentEntry.Sequence,
                    SourceRowNumber = parentEntry.SourceRowNumber, Name = parentEntry.Name,
                    MunicipalityId = municipalityId,
                    Deliveries = parentEntry.Deliveries, AveragePerDay = parentEntry.AveragePerDay,
                    IsExcludedFromOptimization = parentEntry.IsExcludedFromOptimization,
                    Note = parentEntry.Note, CreatedAt = now
                };
                if (entryCorrections.TryGetValue(parentEntry.Id, out var ownCorrections))
                {
                    var weight = ownCorrections.LastOrDefault(item => item.Kind == RouteImportCorrectionKinds.SetWeight);
                    if (weight is not null) entry.AveragePerDay = RouteLoadPolicy.Normalize(weight.CorrectedWeightKg!.Value);
                    var exclusion = ownCorrections.LastOrDefault(item => item.Kind == RouteImportCorrectionKinds.ExcludeStop);
                    if (exclusion is not null)
                    {
                        if (parentEntry.AveragePerDay != 0)
                            throw new StructuralImportException("Somente uma parada com peso exatamente zero pode ser excluída da simulação.");
                        entry.IsExcludedFromOptimization = true;
                    }
                }
                route.Entries.Add(entry);
            }
            RecalculateRoute(route, capacity);
            dbContext.Routes.Add(route);
        }

        var affectedWeekdays = ResolveAffectedWeekdays(import, parentRoutes, corrections);
        await CopyUnchangedResults(parentId, import.Id, affectedWeekdays,
            routeMap, entryMap, cancellationToken);
        import.TotalRows = parent.TotalRows;
        import.ImportedRows = parent.ImportedRows;
        import.ErrorCount = 0;
        import.Status = RouteImportStatus.Completed;
        import.FinishedAt = now;
        import.FailureMessage = null;
        source.CurrentImportId = import.Id;
        source.LastSuccessfulImportId = import.Id;
        source.StateUpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyCatalogCorrections(
        RouteImport import,
        IReadOnlyCollection<RouteImportCorrection> corrections,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var correction in corrections.Where(item => item.Kind == RouteImportCorrectionKinds.SetVehicleTypeCapacity))
        {
            if (correction.VehicleTypeId is null || correction.CorrectedCapacityKg is null or <= 0)
                throw new StructuralImportException("A capacidade do tipo de veículo deve ser positiva.");
            var vehicleType = await dbContext.VehicleTypes.SingleAsync(item => item.Id == correction.VehicleTypeId, cancellationToken);
            vehicleType.CapacityKg = LogisticsVehicleCapacityPolicy.Normalize(correction.CorrectedCapacityKg.Value);
        }

        foreach (var correction in corrections.Where(item => item.Kind is
                     RouteImportCorrectionKinds.ConfirmOfficialCoordinate or RouteImportCorrectionKinds.SetManualCoordinate ||
                     item.Kind == RouteImportCorrectionKinds.LinkMunicipality &&
                     item.CorrectedLatitude.HasValue && item.CorrectedLongitude.HasValue))
        {
            if (correction.MunicipalityId is null || correction.CorrectedLatitude is not >= -90 or not <= 90 ||
                correction.CorrectedLongitude is not >= -180 or not <= 180)
                throw new StructuralImportException("A latitude ou longitude informada é inválida.");
            var coordinate = await dbContext.MunicipalityCoordinates
                .SingleOrDefaultAsync(item => item.MunicipalityId == correction.MunicipalityId, cancellationToken);
            if (coordinate is null)
            {
                coordinate = new MunicipalityCoordinate { Id = Guid.NewGuid(), MunicipalityId = correction.MunicipalityId.Value, CreatedAt = now };
                dbContext.MunicipalityCoordinates.Add(coordinate);
            }
            coordinate.Latitude = correction.CorrectedLatitude;
            coordinate.Longitude = correction.CorrectedLongitude;
            coordinate.Status = MunicipalityCoordinateStatuses.Resolved;
            coordinate.Source = correction.Kind == RouteImportCorrectionKinds.SetManualCoordinate
                ? "MANUAL_AUDITADO"
                : municipalityProvider?.SourceName ?? EmbeddedMunicipalityCoordinateProvider.Source;
            coordinate.ResolvedAt = now;
            coordinate.UpdatedAt = now;
            coordinate.FailureReason = null;
        }

        foreach (var correction in corrections.Where(item => item.Kind == RouteImportCorrectionKinds.LinkMunicipality))
        {
            if (correction.MunicipalityId is null || string.IsNullOrWhiteSpace(correction.SourceLabel))
                throw new StructuralImportException("O vínculo municipal informado é inválido.");
            var normalized = MunicipalityNameNormalizer.Normalize(correction.SourceLabel);
            var alias = await dbContext.MunicipalityAliases
                .SingleOrDefaultAsync(item => item.DataSourceId == import.DataSourceId && item.NormalizedAlias == normalized,
                    cancellationToken);
            if (alias is null)
            {
                dbContext.MunicipalityAliases.Add(new MunicipalityAlias
                {
                    Id = Guid.NewGuid(), DataSourceId = import.DataSourceId, Alias = correction.SourceLabel,
                    NormalizedAlias = normalized, MunicipalityId = correction.MunicipalityId.Value,
                    CreatedByUserId = correction.RequestedByUserId, UpdatedByUserId = correction.RequestedByUserId,
                    CreatedAt = now, UpdatedAt = now
                });
            }
            else if (alias.MunicipalityId != correction.MunicipalityId)
            {
                alias.MunicipalityId = correction.MunicipalityId.Value;
                alias.Alias = correction.SourceLabel;
                alias.UpdatedByUserId = correction.RequestedByUserId;
                alias.UpdatedAt = now;
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlySet<string> ResolveAffectedWeekdays(
        RouteImport import,
        IReadOnlyCollection<Route> parentRoutes,
        IReadOnlyCollection<RouteImportCorrection> corrections)
    {
        var affected = import.AffectedWeekdays.Select(item => item.Weekday).ToHashSet(StringComparer.Ordinal);
        var routeById = parentRoutes.ToDictionary(item => item.Id);
        var entryRoutes = parentRoutes.SelectMany(route => route.Entries.Select(entry => new { entry.Id, Route = route }))
            .ToDictionary(item => item.Id, item => item.Route);
        foreach (var correction in corrections)
        {
            if (correction.SourceRouteEntryId.HasValue && entryRoutes.TryGetValue(correction.SourceRouteEntryId.Value, out var entryRoute))
                affected.Add(entryRoute.Weekday);
            else if (correction.SourceRouteId.HasValue && routeById.TryGetValue(correction.SourceRouteId.Value, out var route))
                affected.Add(route.Weekday);
            if (correction.VehicleTypeId.HasValue)
                foreach (var day in parentRoutes.Where(item => item.VehicleTypeId == correction.VehicleTypeId).Select(item => item.Weekday))
                    affected.Add(day);
            var changesCoordinate = correction.Kind is RouteImportCorrectionKinds.ConfirmOfficialCoordinate or
                RouteImportCorrectionKinds.SetManualCoordinate ||
                correction.Kind == RouteImportCorrectionKinds.LinkMunicipality &&
                (correction.OriginalLatitude != correction.CorrectedLatitude ||
                 correction.OriginalLongitude != correction.CorrectedLongitude);
            if (changesCoordinate && correction.MunicipalityId.HasValue)
                foreach (var day in parentRoutes.Where(item => item.Entries.Any(entry => entry.MunicipalityId == correction.MunicipalityId))
                             .Select(item => item.Weekday))
                    affected.Add(day);
            if (correction.Kind == RouteImportCorrectionKinds.LinkMunicipality && !string.IsNullOrWhiteSpace(correction.SourceLabel))
            {
                var normalizedAlias = MunicipalityNameNormalizer.Normalize(correction.SourceLabel);
                foreach (var day in parentRoutes.Where(item => item.Entries.Any(entry =>
                                 MunicipalityNameNormalizer.Normalize(entry.Name) == normalizedAlias)).Select(item => item.Weekday))
                    affected.Add(day);
            }
        }
        foreach (var day in affected.Where(day => import.AffectedWeekdays.All(item => item.Weekday != day)))
            import.AffectedWeekdays.Add(new RouteImportAffectedWeekday { ImportId = import.Id, Weekday = day });
        return affected;
    }

    private async Task CopyUnchangedResults(
        Guid parentId,
        Guid childId,
        IReadOnlySet<string> affectedWeekdays,
        IReadOnlyDictionary<Guid, Guid> routeMap,
        IReadOnlyDictionary<Guid, Guid> entryMap,
        CancellationToken cancellationToken)
    {
        var originals = await dbContext.DailyRouteOptimizationResults.AsNoTracking()
            .Where(item => item.RouteImportId == parentId && !affectedWeekdays.Contains(item.Weekday))
            .Include(item => item.Issues)
            .Include(item => item.Vehicles).ThenInclude(item => item.Stops)
            .ToListAsync(cancellationToken);
        foreach (var original in originals)
        {
            var resultId = Guid.NewGuid();
            var copy = new DailyRouteOptimizationResult
            {
                Id = resultId, RouteImportId = childId, JobExecutionId = original.JobExecutionId,
                InheritedFromResultId = original.Id, Weekday = original.Weekday, Status = original.Status,
                Reason = original.Reason, CurrentDistanceMeters = original.CurrentDistanceMeters,
                CurrentDurationSeconds = original.CurrentDurationSeconds, ProposedDistanceMeters = original.ProposedDistanceMeters,
                ProposedDurationSeconds = original.ProposedDurationSeconds, CurrentVehicleCount = original.CurrentVehicleCount,
                ProposedVehicleCount = original.ProposedVehicleCount, AdditionalVehicleCount = original.AdditionalVehicleCount,
                AdditionalCapacityKg = original.AdditionalCapacityKg, TotalWeightKg = original.TotalWeightKg,
                CreatedAt = original.CreatedAt
            };
            copy.Issues = original.Issues.Select(issue => new DailyRouteOptimizationIssue
            {
                Id = Guid.NewGuid(), ResultId = resultId, Code = issue.Code,
                RouteId = MapId(routeMap, issue.RouteId),
                RouteEntryId = MapId(entryMap, issue.RouteEntryId),
                MunicipalityId = issue.MunicipalityId, VehicleTypeId = issue.VehicleTypeId,
                Message = issue.Message, CurrentValue = issue.CurrentValue, CanResolve = issue.CanResolve
            }).ToArray();
            copy.Vehicles = original.Vehicles.Select(vehicle =>
            {
                var vehicleId = Guid.NewGuid();
                return new DailyRouteOptimizationVehicle
                {
                    Id = vehicleId, ResultId = resultId, VehicleTypeId = vehicle.VehicleTypeId,
                    SourceRouteId = MapId(routeMap, vehicle.SourceRouteId),
                    Sequence = vehicle.Sequence, IsAdditional = vehicle.IsAdditional, IsIdle = vehicle.IsIdle,
                    CapacityKg = vehicle.CapacityKg, LoadKg = vehicle.LoadKg, Occupancy = vehicle.Occupancy,
                    DistanceMeters = vehicle.DistanceMeters, DurationSeconds = vehicle.DurationSeconds,
                    Stops = vehicle.Stops.Select(stop => new DailyRouteOptimizationStop
                    {
                        Id = Guid.NewGuid(), VehicleId = vehicleId, MunicipalityId = stop.MunicipalityId,
                        Sequence = stop.Sequence, WeightKg = stop.WeightKg,
                        DistanceFromPreviousMeters = stop.DistanceFromPreviousMeters,
                        DurationFromPreviousSeconds = stop.DurationFromPreviousSeconds
                    }).ToArray()
                };
            }).ToArray();
            dbContext.DailyRouteOptimizationResults.Add(copy);
        }
    }

    private static void RecalculateRoute(Route route, decimal capacityKg)
    {
        route.TotalWeightKg = route.Entries.Where(item => !item.IsExcludedFromOptimization).Sum(item => item.AveragePerDay);
        var occupancy = RouteOccupancyCalculator.Calculate(new RouteOccupancyInput(
            route.TotalWeightKg, capacityKg, route.TotalVolumeM3, route.VehicleType?.CapacityVolumeM3,
            route.TotalPallets, route.VehicleType?.CapacityPallets));
        route.WeightOccupancy = occupancy.WeightOccupancy;
        route.VolumeOccupancy = occupancy.VolumeOccupancy;
        route.PalletOccupancy = occupancy.PalletOccupancy;
        route.OverallOccupancy = occupancy.OverallOccupancy;
        route.OccupancyStatus = occupancy.HasAvailableCapacity
            ? RouteOccupancyStatus.Calculated
            : RouteOccupancyStatus.MissingCapacity;
    }

    private static Guid? MapId(IReadOnlyDictionary<Guid, Guid> ids, Guid? sourceId) =>
        sourceId.HasValue && ids.TryGetValue(sourceId.Value, out var mappedId) ? mappedId : null;

    private static Municipality CreateOfficialMunicipality(
        MunicipalityCoordinateLookup lookup,
        string source,
        DateTime now)
    {
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(), StateCode = lookup.StateCode, Name = lookup.Name,
            NormalizedName = lookup.NormalizedName, IbgeCode = lookup.IbgeCode, CreatedAt = now
        };
        municipality.Coordinate = new MunicipalityCoordinate
        {
            Id = Guid.NewGuid(), MunicipalityId = municipality.Id, Source = source,
            Status = MunicipalityCoordinateStatuses.Resolved, Latitude = lookup.Latitude,
            Longitude = lookup.Longitude, ResolvedAt = now, CreatedAt = now, UpdatedAt = now
        };
        return municipality;
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
