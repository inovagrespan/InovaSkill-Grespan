using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class RouteCostConsolidationProcessor(
    ImportDbContext db,
    IRouteGeometryClient routeGeometryClient,
    ITollCatalog tollCatalog) : IProgressReportingOperationalJobProcessor
{
    public string JobType => OperationalJobCodes.RouteCostConsolidation;

    public Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken) =>
        ProcessAsync(relatedEntityId, Guid.Empty, cancellationToken);

    public async Task ProcessAsync(Guid routeImportId, Guid jobExecutionId, CancellationToken cancellationToken)
    {
        var dieselPrice = await db.LogisticsFuelSettings.AsNoTracking()
            .Select(item => (decimal?)item.DieselPricePerLiter).SingleOrDefaultAsync(cancellationToken);
        var depot = await db.LogisticsDepots.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("O centro de distribuição não está configurado.");
        var routes = await db.Routes.AsNoTracking()
            .Where(route => route.ImportId == routeImportId)
            .Include(route => route.VehicleType)
            .Include(route => route.Entries)
            .Include(route => route.CustomerAssignments)
                .ThenInclude(assignment => assignment.Customer)
                .ThenInclude(customer => customer!.RegistrationAddress)
                .ThenInclude(address => address!.Coordinate)
            .OrderBy(route => route.Weekday).ThenBy(route => route.Name)
            .ToListAsync(cancellationToken);
        if (routes.Count == 0) throw new InvalidOperationException("O snapshot publicado não possui rotas.");
        var optimizationResults = await db.DailyRouteOptimizationResults.AsNoTracking()
            .Where(result => result.RouteImportId == routeImportId)
            .Include(result => result.Vehicles).ThenInclude(vehicle => vehicle.VehicleType)
            .Include(result => result.Vehicles).ThenInclude(vehicle => vehicle.Stops)
                .ThenInclude(stop => stop.Municipality).ThenInclude(municipality => municipality!.Coordinate)
            .OrderBy(result => result.Weekday).ToListAsync(cancellationToken);

        var fingerprint = BuildFingerprint(routes, optimizationResults, dieselPrice, depot, tollCatalog.Current.Version);
        var existing = await db.RouteCostSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(snapshot => snapshot.RouteImportId == routeImportId, cancellationToken);
        if (existing?.InputFingerprint == fingerprint)
        {
            await SetJobResult(jobExecutionId, existing.Id, fingerprint, reused: true, cancellationToken);
            return;
        }

        var snapshot = new RouteCostSnapshot
        {
            Id = Guid.NewGuid(), RouteImportId = routeImportId, JobExecutionId = jobExecutionId,
            InputFingerprint = fingerprint, DieselPricePerLiter = dieselPrice,
            TollCatalogVersion = tollCatalog.Current.Version, TollEffectiveFrom = tollCatalog.Current.EffectiveFrom,
            CalculatedAt = DateTime.UtcNow
        };
        foreach (var route in routes)
            snapshot.Items.Add(await BuildActualItem(snapshot.Id, route, depot, dieselPrice, cancellationToken));
        foreach (var weekday in routes.Select(route => route.Weekday).Distinct(StringComparer.Ordinal))
        {
            var result = optimizationResults.SingleOrDefault(item => item.Weekday == weekday);
            if (result is null || result.Vehicles.All(vehicle => vehicle.IsIdle))
            {
                snapshot.Items.Add(UnavailableOptimizedItem(snapshot.Id, weekday, result,
                    result is null ? "Não existe otimização consolidada para o dia." : "A otimização não possui veículos ativos."));
                continue;
            }
            foreach (var vehicle in result.Vehicles.Where(vehicle => !vehicle.IsIdle).OrderBy(vehicle => vehicle.Sequence))
                snapshot.Items.Add(await BuildOptimizedItem(snapshot.Id, result, vehicle, depot, dieselPrice, cancellationToken));
        }

        IDbContextTransaction? transaction = null;
        if (db.Database.IsRelational()) transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (existing is not null)
            {
                var tracked = await db.RouteCostSnapshots.SingleAsync(item => item.Id == existing.Id, cancellationToken);
                db.RouteCostSnapshots.Remove(tracked);
                await db.SaveChangesAsync(cancellationToken);
            }
            db.RouteCostSnapshots.Add(snapshot);
            await db.SaveChangesAsync(cancellationToken);
            await SetJobResult(jobExecutionId, snapshot.Id, fingerprint, reused: false, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private async Task<RouteCostItem> BuildActualItem(Guid snapshotId, Route route, LogisticsDepot depot,
        decimal? dieselPrice, CancellationToken cancellationToken)
    {
        var item = NewItem(snapshotId, RouteCostScenarios.Actual, route.Weekday, route.Name,
            RouteCostPathBases.ExactCustomers, route.VehicleTypeId);
        item.RouteId = route.Id;
        if (dieselPrice is null or <= 0) return Unavailable(item, "O preço do diesel não está configurado.");
        var configurationError = ValidateConfiguration(route.VehicleType);
        if (configurationError is not null) return Unavailable(item, configurationError);
        var assignments = route.CustomerAssignments.OrderBy(assignment =>
                route.Entries.Where(entry => entry.MunicipalityId == assignment.MunicipalityId)
                    .Select(entry => entry.Sequence).DefaultIfEmpty(int.MaxValue).Min())
            .ThenBy(assignment => assignment.Customer!.ExternalCode, StringComparer.Ordinal).ToArray();
        if (assignments.Length == 0) return Unavailable(item, "A rota não possui clientes vinculados.");
        if (assignments.Any(assignment => !HasExactCoordinate(assignment.Customer)))
            return Unavailable(item, "Há clientes sem coordenadas exatas no percurso.");
        var points = new List<RouteGeometryPoint> { DepotPoint(depot) };
        points.AddRange(assignments.Select(assignment =>
        {
            var coordinate = assignment.Customer!.RegistrationAddress!.Coordinate!;
            return new RouteGeometryPoint(assignment.CustomerId, assignment.Customer.ExternalCode,
                coordinate.Latitude!.Value, coordinate.Longitude!.Value);
        }));
        points.Add(DepotPoint(depot));
        return await Calculate(item, points, route.VehicleType!, dieselPrice.Value, cancellationToken);
    }

    private async Task<RouteCostItem> BuildOptimizedItem(Guid snapshotId, DailyRouteOptimizationResult result,
        DailyRouteOptimizationVehicle vehicle, LogisticsDepot depot, decimal? dieselPrice, CancellationToken cancellationToken)
    {
        var item = NewItem(snapshotId, RouteCostScenarios.Optimized, result.Weekday,
            $"Veículo otimizado {vehicle.Sequence + 1}", RouteCostPathBases.OptimizedMunicipalities, vehicle.VehicleTypeId);
        item.OptimizationResultId = result.Id;
        item.OptimizationVehicleId = vehicle.Id;
        if (dieselPrice is null or <= 0) return Unavailable(item, "O preço do diesel não está configurado.");
        var configurationError = ValidateConfiguration(vehicle.VehicleType);
        if (configurationError is not null) return Unavailable(item, configurationError);
        var stops = vehicle.Stops.OrderBy(stop => stop.Sequence).ToArray();
        if (stops.Length == 0) return Unavailable(item, "O veículo otimizado não possui paradas.");
        if (stops.Any(stop => stop.Municipality?.Coordinate?.Latitude is null || stop.Municipality.Coordinate.Longitude is null))
            return Unavailable(item, "Há municípios sem coordenadas no percurso otimizado.");
        var points = new List<RouteGeometryPoint> { DepotPoint(depot) };
        points.AddRange(stops.Select(stop => new RouteGeometryPoint(stop.MunicipalityId,
            stop.Municipality!.Name, stop.Municipality.Coordinate!.Latitude!.Value,
            stop.Municipality.Coordinate.Longitude!.Value)));
        points.Add(DepotPoint(depot));
        return await Calculate(item, points, vehicle.VehicleType!, dieselPrice.Value, cancellationToken);
    }

    private async Task<RouteCostItem> Calculate(RouteCostItem item, IReadOnlyList<RouteGeometryPoint> points,
        VehicleType vehicleType, decimal dieselPrice, CancellationToken cancellationToken)
    {
        var route = await routeGeometryClient.GetRouteAsync(points, cancellationToken);
        var fuel = RouteCostPolicy.CalculateFuel(route.DistanceMeters,
            vehicleType.MinimumFuelEfficiencyKmPerLiter!.Value,
            vehicleType.MaximumFuelEfficiencyKmPerLiter!.Value, dieselPrice);
        var toll = tollCatalog.Estimate(route.Geometry, vehicleType.AxleCount!.Value);
        item.IsAvailable = true;
        item.DistanceMeters = decimal.Round(route.DistanceMeters, 3, MidpointRounding.AwayFromZero);
        item.DurationSeconds = decimal.Round(route.DurationSeconds, 3, MidpointRounding.AwayFromZero);
        item.MinimumFuelLiters = fuel.MinimumLiters;
        item.MaximumFuelLiters = fuel.MaximumLiters;
        item.MinimumFuelCost = fuel.MinimumCost;
        item.MaximumFuelCost = fuel.MaximumCost;
        item.TollCost = toll.TotalCost;
        item.TollPassages = toll.TotalPassages;
        item.MinimumTotalCost = RouteCostPolicy.RoundCurrency(fuel.MinimumCost + toll.TotalCost);
        item.MaximumTotalCost = RouteCostPolicy.RoundCurrency(fuel.MaximumCost + toll.TotalCost);
        item.TollPassageItems = toll.Passages.Select(passage => new RouteCostTollPassage
        {
            Id = Guid.NewGuid(), RouteCostItemId = item.Id, TollPlazaCode = passage.Plaza.Code,
            TollPlazaName = passage.Plaza.Name, OperatorName = passage.Plaza.OperatorName,
            Highway = passage.Plaza.Highway, Kilometer = passage.Plaza.Kilometer,
            AxleCount = passage.AxleCount, Passages = passage.Passages,
            AutomaticUnitTariff = passage.AutomaticUnitTariff, TotalCost = passage.TotalCost
        }).ToArray();
        return item;
    }

    private static RouteCostItem NewItem(Guid snapshotId, string scenario, string weekday, string label,
        string pathBasis, Guid? vehicleTypeId) => new()
        {
            Id = Guid.NewGuid(), SnapshotId = snapshotId, Scenario = scenario, Weekday = weekday,
            Label = label, PathBasis = pathBasis, VehicleTypeId = vehicleTypeId
        };

    private static RouteCostItem UnavailableOptimizedItem(Guid snapshotId, string weekday,
        DailyRouteOptimizationResult? result, string reason)
    {
        var item = NewItem(snapshotId, RouteCostScenarios.Optimized, weekday, "Cenário otimizado",
            RouteCostPathBases.OptimizedMunicipalities, null);
        item.OptimizationResultId = result?.Id;
        return Unavailable(item, reason);
    }

    private static RouteCostItem Unavailable(RouteCostItem item, string reason)
    {
        item.IsAvailable = false;
        item.UnavailableReason = reason;
        return item;
    }

    private static string? ValidateConfiguration(VehicleType? vehicleType)
    {
        if (vehicleType?.AxleCount is null || vehicleType.MinimumFuelEfficiencyKmPerLiter is null ||
            vehicleType.MaximumFuelEfficiencyKmPerLiter is null)
            return "O tipo de veículo não possui eixos e consumo configurados.";
        try
        {
            RouteCostPolicy.ValidateVehicleConfiguration(vehicleType.AxleCount.Value,
                vehicleType.MinimumFuelEfficiencyKmPerLiter.Value, vehicleType.MaximumFuelEfficiencyKmPerLiter.Value);
            return null;
        }
        catch (ArgumentException exception) { return exception.Message; }
    }

    private static bool HasExactCoordinate(Customer? customer) =>
        customer?.RegistrationAddress?.Coordinate is
        {
            Status: CustomerAddressCoordinateStatuses.Resolved,
            Precision: CustomerAddressCoordinatePrecisions.Exact,
            Latitude: not null,
            Longitude: not null
        };

    private static RouteGeometryPoint DepotPoint(LogisticsDepot depot) =>
        new(depot.Id, depot.Name, depot.Latitude, depot.Longitude);

    private static string BuildFingerprint(IReadOnlyList<Route> routes,
        IReadOnlyList<DailyRouteOptimizationResult> optimizationResults, decimal? dieselPrice,
        LogisticsDepot depot, string tollVersion)
    {
        var values = new List<string>
        {
            dieselPrice?.ToString(CultureInfo.InvariantCulture) ?? "NO_DIESEL", tollVersion,
            depot.Latitude.ToString(CultureInfo.InvariantCulture), depot.Longitude.ToString(CultureInfo.InvariantCulture)
        };
        values.AddRange(routes.Select(route =>
        {
            var effectiveAssignments = route.CustomerAssignments.OrderBy(assignment =>
                    route.Entries.Where(entry => entry.MunicipalityId == assignment.MunicipalityId)
                        .Select(entry => entry.Sequence).DefaultIfEmpty(int.MaxValue).Min())
                .ThenBy(assignment => assignment.Customer!.ExternalCode, StringComparer.Ordinal);
            return string.Join('|', route.Id, route.Weekday, route.VehicleTypeId,
                route.VehicleType?.AxleCount, route.VehicleType?.MinimumFuelEfficiencyKmPerLiter,
                route.VehicleType?.MaximumFuelEfficiencyKmPerLiter,
                string.Join(',', route.Entries.OrderBy(entry => entry.Sequence)
                    .Select(entry => $"{entry.Id}:{entry.Sequence}:{entry.MunicipalityId}")),
                string.Join(',', effectiveAssignments.Select(assignment =>
                    $"{assignment.CustomerId}:{assignment.MunicipalityId}:{assignment.Customer?.ExternalCode}:{assignment.Customer?.RegistrationAddress?.Coordinate?.Latitude}:{assignment.Customer?.RegistrationAddress?.Coordinate?.Longitude}:{assignment.Customer?.RegistrationAddress?.Coordinate?.Precision}")));
        }));
        values.AddRange(optimizationResults.Select(result =>
        {
            var vehicles = result.Vehicles.OrderBy(vehicle => vehicle.Sequence).Select(vehicle =>
            {
                var stops = string.Join(';', vehicle.Stops.OrderBy(stop => stop.Sequence).Select(stop =>
                    $"{stop.Sequence}:{stop.MunicipalityId}:{stop.Municipality?.Coordinate?.Latitude}:{stop.Municipality?.Coordinate?.Longitude}"));
                return $"{vehicle.Id}:{vehicle.Sequence}:{vehicle.VehicleTypeId}:{vehicle.VehicleType?.AxleCount}:{vehicle.VehicleType?.MinimumFuelEfficiencyKmPerLiter}:{vehicle.VehicleType?.MaximumFuelEfficiencyKmPerLiter}:{vehicle.IsIdle}:{stops}";
            });
            return string.Join('|', result.Id, result.Weekday, result.Status, string.Join(',', vehicles));
        }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values)))).ToLowerInvariant();
    }

    private async Task SetJobResult(Guid jobId, Guid snapshotId, string fingerprint, bool reused,
        CancellationToken cancellationToken)
    {
        if (jobId == Guid.Empty) return;
        var job = await db.JobExecutions.SingleAsync(item => item.Id == jobId, cancellationToken);
        job.ResultJson = JsonSerializer.Serialize(new { snapshotId, fingerprint, reused });
        job.ProgressMessage = reused ? "Snapshot de custos já estava atualizado" : "Custos de rotas consolidados";
        await db.SaveChangesAsync(cancellationToken);
    }
}
