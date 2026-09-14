using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class DailyRouteOptimizationProcessor(
    ImportDbContext db,
    IOsrmDailyMatrixService matrixService,
    IDailyRouteOptimizationSolver solver) : IProgressReportingOperationalJobProcessor
{
    public DailyRouteOptimizationProcessor(
        ImportDbContext db,
        IOsrmDailyMatrixService matrixService,
        IDailyRouteOptimizationSolver solver,
        IMunicipalityCoordinateProvider municipalityProvider)
        : this(db, matrixService, solver)
    {
    }

    public string JobType => OperationalJobCodes.DailyRouteOptimization;

    public Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken) =>
        ProcessAsync(relatedEntityId, Guid.Empty, cancellationToken);

    public async Task ProcessAsync(Guid routeImportId, Guid jobExecutionId, CancellationToken cancellationToken)
    {
        var availableWeekdays = await db.Routes.AsNoTracking()
            .Where(route => route.ImportId == routeImportId)
            .Select(route => route.Weekday).Distinct().OrderBy(day => day).ToListAsync(cancellationToken);
        if (availableWeekdays.Count == 0) throw new InvalidOperationException("O snapshot publicado não possui rotas.");
        var weekdays = await ResolveRequestedWeekdays(jobExecutionId, availableWeekdays, cancellationToken);

        for (var index = 0; index < weekdays.Count; index++)
        {
            var weekday = weekdays[index];
            await UpdateProgress(jobExecutionId, index, weekdays.Count, $"Otimizando {weekday}", cancellationToken);
            await ProcessDay(routeImportId, jobExecutionId, weekday, cancellationToken);
        }
        var job = await db.JobExecutions.SingleAsync(item => item.Id == jobExecutionId, cancellationToken);
        job.ResultJson = JsonSerializer.Serialize(new { routeImportId, processedDays = weekdays });
        job.ProgressMessage = $"{weekdays.Count} dia(s) simulados";
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessDay(Guid importId, Guid jobExecutionId, string weekday, CancellationToken cancellationToken)
    {
        var routes = await db.Routes
            .Where(route => route.ImportId == importId && route.Weekday == weekday)
            .Include(route => route.VehicleType)
            .Include(route => route.Entries).ThenInclude(entry => entry.Municipality).ThenInclude(item => item!.Coordinate)
            .OrderBy(route => route.Name).ToListAsync(cancellationToken);
        var includedEntries = routes.SelectMany(route => route.Entries)
            .Where(entry => !entry.IsExcludedFromOptimization).ToArray();
        var totalWeight = includedEntries.Sum(entry => entry.AveragePerDay);
        var issues = DailyRouteOptimizationReadinessEvaluator.Evaluate(routes);
        if (issues.Count > 0)
        {
            var resultId = Guid.NewGuid();
            await ReplaceResult(new DailyRouteOptimizationResult
            {
                Id = resultId, RouteImportId = importId, JobExecutionId = jobExecutionId,
                Weekday = weekday, Status = DailyRouteOptimizationStatuses.InsufficientData,
                Reason = DailyRouteOptimizationReadinessEvaluator.Summarize(issues),
                CurrentVehicleCount = routes.Count, TotalWeightKg = totalWeight,
                CreatedAt = DateTime.UtcNow,
                Issues = issues.Select(issue => new DailyRouteOptimizationIssue
                {
                    Id = Guid.NewGuid(), ResultId = resultId, Code = issue.Code,
                    RouteId = issue.RouteId, RouteEntryId = issue.RouteEntryId,
                    MunicipalityId = issue.MunicipalityId, VehicleTypeId = issue.VehicleTypeId,
                    Message = issue.Message, CurrentValue = issue.CurrentValue, CanResolve = issue.CanResolve
                }).ToArray()
            }, cancellationToken);
            return;
        }

        var aggregatedBlocks = includedEntries
            .GroupBy(entry => entry.MunicipalityId!.Value)
            .Select(group => new RouteOptimizationBlock(
                group.Key, group.First().Municipality!.Name, ToGrams(group.Sum(entry => entry.AveragePerDay))))
            .OrderBy(block => block.Name).ThenBy(block => block.MunicipalityId).ToArray();
        var vehicleTypes = await db.VehicleTypes.AsNoTracking().Where(type => type.CapacityKg > 0)
            .OrderBy(type => type.CapacityKg).ThenBy(type => type.Name).ToListAsync(cancellationToken);
        if (vehicleTypes.Count == 0)
        {
            await ReplaceResult(new DailyRouteOptimizationResult
            {
                Id = Guid.NewGuid(), RouteImportId = importId, JobExecutionId = jobExecutionId,
                Weekday = weekday, Status = DailyRouteOptimizationStatuses.Infeasible,
                Reason = "Não há tipo de veículo com capacidade válida cadastrado.",
                CurrentVehicleCount = routes.Count, TotalWeightKg = totalWeight, CreatedAt = DateTime.UtcNow
            }, cancellationToken);
            return;
        }
        var maximumCapacityGrams = ToGrams(vehicleTypes.Max(type => type.CapacityKg!.Value));
        var blocks = SplitOversizedMunicipalBlocks(aggregatedBlocks, maximumCapacityGrams);

        var matrix = await matrixService.GetForDayAsync(importId, weekday, cancellationToken);
        var existing = routes.Select(route => new RouteOptimizationVehicleInput(
            route.Id, route.VehicleTypeId, route.VehicleType!.Name,
            ToGrams(route.VehicleCapacityKgSnapshot), false)).ToArray();
        var additional = vehicleTypes.Select(type => new RouteOptimizationVehicleInput(
            null, type.Id, type.Name, ToGrams(type.CapacityKg!.Value), true)).ToArray();
        var problem = new RouteOptimizationProblem(weekday, blocks, existing, additional, matrix);
        var solution = solver.Solve(problem);
        DailyRouteOptimizationSolutionValidator.Validate(problem, solution);
        var current = CurrentMetrics(routes, matrix);
        var currentIsFeasible = routes.All(route =>
            route.TotalWeightKg - route.Entries.Where(entry => entry.IsExcludedFromOptimization)
                .Sum(entry => entry.AveragePerDay) <=
            route.VehicleCapacityKgSnapshot);
        var improved = !currentIsFeasible || solution.TotalDistanceMeters < current.Distance;
        var status = solution.Status == DailyRouteOptimizationStatuses.Optimized && !improved
            ? DailyRouteOptimizationStatuses.NoImprovement
            : solution.Status;
        var activeVehicles = solution.Vehicles.Where(vehicle => vehicle.Stops.Count > 0).ToArray();
        var activeAdditionalVehicles = activeVehicles.Where(vehicle => vehicle.Vehicle.IsAdditional).ToArray();
        var result = new DailyRouteOptimizationResult
        {
            Id = Guid.NewGuid(), RouteImportId = importId, JobExecutionId = jobExecutionId,
            Weekday = weekday, Status = status,
            Reason = status == DailyRouteOptimizationStatuses.NoImprovement
                ? "A frota atual já é viável e nenhuma redução de distância foi encontrada." : solution.Reason,
            CurrentDistanceMeters = current.Distance, CurrentDurationSeconds = current.Duration,
            ProposedDistanceMeters = solution.TotalDistanceMeters, ProposedDurationSeconds = solution.TotalDurationSeconds,
            CurrentVehicleCount = routes.Count, ProposedVehicleCount = activeVehicles.Length,
            AdditionalVehicleCount = activeAdditionalVehicles.Length,
            AdditionalCapacityKg = FromGrams(activeAdditionalVehicles.Sum(vehicle => vehicle.Vehicle.CapacityGrams)),
            TotalWeightKg = totalWeight, CreatedAt = DateTime.UtcNow
        };
        if (status == DailyRouteOptimizationStatuses.Optimized)
            result.Vehicles = BuildVehicles(result.Id, solution, blocks);
        await ReplaceResult(result, cancellationToken);
    }

    private static ICollection<DailyRouteOptimizationVehicle> BuildVehicles(
        Guid resultId, RouteOptimizationSolution solution, IReadOnlyList<RouteOptimizationBlock> blocks) =>
        solution.Vehicles.Select((vehicle, sequence) => new DailyRouteOptimizationVehicle
        {
            Id = Guid.NewGuid(), ResultId = resultId, VehicleTypeId = vehicle.Vehicle.VehicleTypeId,
            SourceRouteId = vehicle.Vehicle.SourceRouteId, Sequence = sequence,
            IsAdditional = vehicle.Vehicle.IsAdditional, IsIdle = vehicle.Stops.Count == 0,
            CapacityKg = FromGrams(vehicle.Vehicle.CapacityGrams), LoadKg = FromGrams(vehicle.LoadGrams),
            Occupancy = vehicle.Vehicle.CapacityGrams == 0 ? 0 : (decimal)vehicle.LoadGrams / vehicle.Vehicle.CapacityGrams,
            DistanceMeters = vehicle.DistanceMeters, DurationSeconds = vehicle.DurationSeconds,
            Stops = vehicle.Stops.Select((stop, stopSequence) => new DailyRouteOptimizationStop
            {
                Id = Guid.NewGuid(), Sequence = stopSequence, MunicipalityId = blocks[stop.BlockIndex].MunicipalityId,
                WeightKg = FromGrams(blocks[stop.BlockIndex].WeightGrams),
                DistanceFromPreviousMeters = stop.DistanceFromPreviousMeters,
                DurationFromPreviousSeconds = stop.DurationFromPreviousSeconds
            }).ToArray()
        }).ToArray();

    private static (decimal Distance, decimal Duration) CurrentMetrics(IReadOnlyList<Route> routes, OsrmTableResult matrix)
    {
        var point = matrix.Points.Select((value, index) => (value, index)).ToDictionary(item => item.value.Id, item => item.index);
        decimal distance = 0, duration = 0;
        foreach (var route in routes)
        {
            var order = route.Entries.Where(entry => !entry.IsExcludedFromOptimization)
                .OrderBy(entry => entry.Sequence).Select(entry => entry.MunicipalityId!.Value).Distinct().ToArray();
            var previous = 0;
            foreach (var municipalityId in order)
            {
                var next = point[municipalityId]; distance += matrix.DistancesMeters[previous][next]; duration += matrix.DurationsSeconds[previous][next]; previous = next;
            }
            distance += matrix.DistancesMeters[previous][0]; duration += matrix.DurationsSeconds[previous][0];
        }
        return (distance, duration);
    }

    private async Task ReplaceResult(DailyRouteOptimizationResult result, CancellationToken cancellationToken)
    {
        var previous = await db.DailyRouteOptimizationResults
            .SingleOrDefaultAsync(item => item.RouteImportId == result.RouteImportId && item.Weekday == result.Weekday, cancellationToken);
        if (previous is not null) db.DailyRouteOptimizationResults.Remove(previous);
        db.DailyRouteOptimizationResults.Add(result);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateProgress(Guid jobId, int index, int total, string message, CancellationToken cancellationToken)
    {
        var job = await db.JobExecutions.SingleAsync(item => item.Id == jobId, cancellationToken);
        job.ProgressPercent = 5 + (int)Math.Floor(index * 90m / total);
        job.ProgressMessage = message;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> ResolveRequestedWeekdays(
        Guid jobExecutionId,
        IReadOnlyList<string> availableWeekdays,
        CancellationToken cancellationToken)
    {
        if (jobExecutionId == Guid.Empty) return availableWeekdays;
        var parameters = await db.JobExecutions.AsNoTracking()
            .Where(item => item.Id == jobExecutionId)
            .Select(item => item.ParametersJson)
            .SingleAsync(cancellationToken);
        using var document = JsonDocument.Parse(parameters);
        var requestedWeekdays = DailyRouteOptimizationWeekdays.Read(document.RootElement);
        if (requestedWeekdays is null)
            return availableWeekdays;
        var selected = requestedWeekdays.ToArray();
        var invalid = selected.Except(availableWeekdays, StringComparer.Ordinal).ToArray();
        if (selected.Length == 0 || invalid.Length > 0)
            throw new InvalidOperationException("Os dias solicitados não pertencem ao snapshot atual.");
        return selected.OrderBy(item => item, StringComparer.Ordinal).ToArray();
    }

    private static long ToGrams(decimal kilograms) => checked(Decimal.ToInt64(decimal.Round(
        kilograms * DailyRouteOptimizationPolicy.WeightScale, 0, MidpointRounding.AwayFromZero)));
    private static decimal FromGrams(long grams) => (decimal)grams / DailyRouteOptimizationPolicy.WeightScale;

    private static IReadOnlyList<RouteOptimizationBlock> SplitOversizedMunicipalBlocks(
        IReadOnlyList<RouteOptimizationBlock> blocks,
        long maximumCapacityGrams) => blocks.SelectMany(block =>
    {
        var fullLoads = block.WeightGrams / maximumCapacityGrams;
        var remainder = block.WeightGrams % maximumCapacityGrams;
        return Enumerable.Range(0, checked((int)(fullLoads + (remainder > 0 ? 1 : 0))))
            .Select(index => block with
            {
                WeightGrams = index < fullLoads ? maximumCapacityGrams : remainder
            });
    }).ToArray();
}
