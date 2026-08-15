using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class RouteOptimizationService(
    ImportDbContext dbContext,
    IRouteOptimizationJobDispatcher jobDispatcher) : IRouteOptimizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RouteOptimizationRunDto> StartOptimizationAsync(
        RouteOptimizationStartRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Scope == RouteOptimizationScope.SingleRoute && request.TargetRouteId is null)
        {
            throw new ArgumentException("TargetRouteId é obrigatório para otimização de rota única.", nameof(request));
        }

        if (request.Scope == RouteOptimizationScope.AllRoutes && request.TargetRouteId is not null)
        {
            throw new ArgumentException("TargetRouteId deve ser nulo para otimização global.", nameof(request));
        }

        var snapshot = request.SnapshotImportId.HasValue
            ? await ResolveSnapshotAsync(request.SnapshotImportId.Value, cancellationToken)
            : await ResolveSnapshotAtDateAsync(request.ReferenceDate, cancellationToken);

        var existing = await dbContext.RouteOptimizationRuns.AsNoTracking()
            .Include(item => item.Scenarios.OrderBy(scenario => scenario.Rank))
            .Where(item =>
                item.Scope == request.Scope &&
                item.TargetRouteId == request.TargetRouteId &&
                item.ReferenceDate == request.ReferenceDate &&
                item.SnapshotImportId == snapshot.ImportId &&
                item.AlgorithmVersion == RouteOptimizationCodes.AlgorithmVersion &&
                item.RulesVersion == RouteOptimizationCodes.RulesVersion)
            .Where(item =>
                item.Status == RouteOptimizationStatus.Pending ||
                item.Status == RouteOptimizationStatus.LoadingData ||
                item.Status == RouteOptimizationStatus.BuildingProblem ||
                item.Status == RouteOptimizationStatus.CalculatingDistanceMatrix ||
                item.Status == RouteOptimizationStatus.SearchingSolutions ||
                item.Status == RouteOptimizationStatus.PersistingResult ||
                item.Status == RouteOptimizationStatus.Completed ||
                item.Status == RouteOptimizationStatus.NoChangeRecommended ||
                item.Status == RouteOptimizationStatus.NoFeasibleSolution ||
                item.Status == RouteOptimizationStatus.InsufficientData)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing, existing.Scenarios);
        }

        var now = DateTime.UtcNow;
        var run = new RouteOptimizationRun
        {
            Id = Guid.NewGuid(),
            Scope = request.Scope,
            TargetRouteId = request.TargetRouteId,
            ReferenceDate = request.ReferenceDate,
            RequestedByUserId = request.RequestedByUserId,
            RequestedFrom = request.RequestedFrom,
            Status = RouteOptimizationStatus.Pending,
            ProgressStage = RouteOptimizationStatus.Pending,
            AlgorithmVersion = RouteOptimizationCodes.AlgorithmVersion,
            RulesVersion = RouteOptimizationCodes.RulesVersion,
            SnapshotImportId = snapshot.ImportId,
            SnapshotImportVersion = snapshot.Version,
            CreatedAt = now
        };
        var job = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobType = RouteOptimizationCodes.JobType,
            ContractVersion = OperationalJobCatalog.RouteOptimization.ContractVersion,
            Queue = BackgroundJobQueues.RouteOptimization,
            Trigger = request.RequestedFrom == RouteOptimizationRequestedFrom.InternalProcess
                ? JobExecutionTrigger.System
                : JobExecutionTrigger.Manual,
            RequestedByUserId = request.RequestedByUserId == 0 ? null : request.RequestedByUserId,
            ParametersJson = JsonSerializer.Serialize(new
            {
                optimizationRunId = run.Id,
                scope = request.Scope.ToString(),
                referenceDate = request.ReferenceDate,
                targetRouteId = request.TargetRouteId,
                snapshotImportId = snapshot.ImportId
            }, JsonOptions),
            Status = JobExecutionStatus.Queued,
            RelatedEntityId = run.Id,
            CreatedAt = now
        };

        dbContext.RouteOptimizationRuns.Add(run);
        dbContext.JobExecutions.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            jobDispatcher.Enqueue(run.Id);
        }
        catch (Exception exception)
        {
            run.Status = RouteOptimizationStatus.Failed;
            run.ProgressStage = RouteOptimizationStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;
            run.ErrorCode = "QueueFailed";
            run.ErrorMessage = "Falha ao enfileirar a simulação de otimização.";
            job.Status = JobExecutionStatus.Failed;
            job.ErrorMessage = $"Falha ao enfileirar no Hangfire: {exception.Message}";
            job.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return ToDto(run, []);
    }

    public async Task<RouteOptimizationRunDto?> GetOptimizationResultAsync(
        Guid optimizationRunId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.RouteOptimizationRuns.AsNoTracking()
            .Include(item => item.Scenarios.OrderBy(scenario => scenario.Rank))
            .SingleOrDefaultAsync(item => item.Id == optimizationRunId, cancellationToken);

        return run is null ? null : ToDto(run, run.Scenarios);
    }

    public async Task<RouteOptimizationRunDto?> GetLatestGlobalOptimizationAsync(
        DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        var query = dbContext.RouteOptimizationRuns.AsNoTracking()
            .Include(item => item.Scenarios.OrderBy(scenario => scenario.Rank))
            .Where(item => item.Scope == RouteOptimizationScope.AllRoutes);

        if (referenceDate.HasValue)
        {
            query = query.Where(item => item.ReferenceDate == referenceDate.Value);
        }

        var run = await query
            .OrderByDescending(item => item.SnapshotImportVersion)
            .ThenByDescending(item => item.CompletedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return run is null ? null : ToDto(run, run.Scenarios);
    }

    public async Task<RouteLatestOptimizationDto> GetLatestRouteOptimizationAsync(
        Guid routeId,
        DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        var currentRoute = await dbContext.Routes.AsNoTracking()
            .Where(route => route.Id == routeId)
            .Select(route => new
            {
                route.Id,
                route.Name,
                route.ImportId,
                route.Import!.Version,
                route.OverallOccupancy,
                route.TotalWeightKg,
                CapacityKg = route.VehicleType!.CapacityKg
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (currentRoute is null)
        {
            return new RouteLatestOptimizationDto(
                RouteOptimizationStatus.InsufficientData,
                null,
                null,
                null,
                null,
                false,
                null,
                "Rota não encontrada.");
        }

        var activeRun = await dbContext.RouteOptimizationRuns.AsNoTracking()
            .Where(run =>
                run.Scope == RouteOptimizationScope.AllRoutes &&
                run.SnapshotImportId == currentRoute.ImportId &&
                (run.Status == RouteOptimizationStatus.Pending ||
                 run.Status == RouteOptimizationStatus.LoadingData ||
                 run.Status == RouteOptimizationStatus.BuildingProblem ||
                 run.Status == RouteOptimizationStatus.CalculatingDistanceMatrix ||
                 run.Status == RouteOptimizationStatus.SearchingSolutions ||
                 run.Status == RouteOptimizationStatus.PersistingResult))
            .OrderByDescending(run => run.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var latestCompleted = await dbContext.RouteOptimizationRuns.AsNoTracking()
            .Include(run => run.Scenarios.OrderBy(scenario => scenario.Rank))
            .Where(run =>
                run.Scope == RouteOptimizationScope.AllRoutes &&
                (run.Status == RouteOptimizationStatus.Completed ||
                 run.Status == RouteOptimizationStatus.NoChangeRecommended ||
                 run.Status == RouteOptimizationStatus.NoFeasibleSolution))
            .OrderByDescending(run => run.SnapshotImportVersion)
            .ThenByDescending(run => run.CompletedAt ?? run.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeRun is not null && latestCompleted is null)
        {
            return new RouteLatestOptimizationDto(
                activeRun.Status,
                activeRun.Id,
                null,
                activeRun.SnapshotImportId,
                activeRun.SnapshotImportVersion,
                false,
                null,
                "Uma nova otimização está sendo processada.");
        }

        if (latestCompleted is null)
        {
            return new RouteLatestOptimizationDto(
                RouteOptimizationStatus.NoFeasibleSolution,
                null,
                null,
                currentRoute.ImportId,
                currentRoute.Version,
                false,
                null,
                "Ainda não existe uma otimização concluída para esta versão dos dados.");
        }

        var projection = BuildRouteProjection(currentRoute.Id, currentRoute.Name, currentRoute.OverallOccupancy,
            currentRoute.CapacityKg, latestCompleted.Scenarios);
        var isStale = latestCompleted.SnapshotImportId != currentRoute.ImportId;
        var message = projection is null
            ? "A otimização mais recente não recomenda alterações para esta rota."
            : "Recomendação encontrada no plano global concluído.";
        if (activeRun is not null && isStale)
        {
            message = "Uma nova otimização está sendo processada. O resultado exibido foi calculado com dados anteriores.";
        }

        return new RouteLatestOptimizationDto(
            activeRun?.Status ?? latestCompleted.Status,
            latestCompleted.Id,
            latestCompleted.CompletedAt,
            latestCompleted.SnapshotImportId,
            latestCompleted.SnapshotImportVersion,
            isStale,
            projection,
            message);
    }

    internal static RouteOptimizationRunDto ToDto(
        RouteOptimizationRun run,
        IEnumerable<RouteOptimizationScenario> scenarios) =>
        new(
            run.Id,
            run.Scope,
            run.TargetRouteId,
            run.ReferenceDate,
            run.RequestedFrom,
            run.Status,
            run.ProgressStage,
            run.ProgressPercentage,
            run.AlgorithmVersion,
            run.RulesVersion,
            run.InputHash,
            run.Confidence,
            run.SnapshotImportId,
            run.SnapshotImportVersion,
            run.CreatedAt,
            run.StartedAt,
            run.CompletedAt,
            run.ErrorCode,
            run.ErrorMessage,
            scenarios.OrderBy(item => item.Rank).Select(ToScenarioDto).ToArray());

    private static RouteOptimizationScenarioDto ToScenarioDto(RouteOptimizationScenario scenario) =>
        new(
            scenario.Id,
            scenario.Rank,
            scenario.Score,
            scenario.ActionType,
            scenario.IsRecommended,
            scenario.Confidence,
            scenario.EstimatedDistanceChangeKm,
            Deserialize<RouteOptimizationMetricsDto>(scenario.CurrentMetricsJson),
            Deserialize<RouteOptimizationMetricsDto>(scenario.ProposedMetricsJson),
            Deserialize<IReadOnlyList<RouteOptimizationReasonDto>>(scenario.ReasonsJson),
            Deserialize<IReadOnlyList<string>>(scenario.WarningsJson),
            Deserialize<IReadOnlyList<RouteCityReallocationDto>>(scenario.CityReallocationsJson),
            string.IsNullOrWhiteSpace(scenario.TruckChangeJson)
                ? null
                : Deserialize<RouteTruckChangeDto>(scenario.TruckChangeJson),
            Deserialize<IReadOnlyList<RouteSequenceOptimizationDto>>(scenario.RouteSequencesJson));

    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException("JSON de cenário de otimização inválido.");

    private static RouteOptimizationRouteProjectionDto? BuildRouteProjection(
        Guid routeId,
        string routeName,
        decimal? currentOccupancy,
        decimal? currentCapacityKg,
        IEnumerable<RouteOptimizationScenario> scenarios)
    {
        var scenario = scenarios.OrderBy(item => item.Rank).FirstOrDefault();
        if (scenario is null) return null;

        var reallocations = Deserialize<IReadOnlyList<RouteCityReallocationDto>>(scenario.CityReallocationsJson);
        var removed = reallocations
            .Where(item => item.SourceRouteId == routeId)
            .Select(item => new RouteOptimizationCityProjectionDto(
                item.CityId,
                item.CityName,
                item.DestinationRouteId,
                item.DestinationRouteName,
                item.CityLoadKg))
            .ToArray();
        var added = reallocations
            .Where(item => item.DestinationRouteId == routeId)
            .Select(item => new RouteOptimizationCityProjectionDto(
                item.CityId,
                item.CityName,
                item.SourceRouteId,
                item.SourceRouteName,
                item.CityLoadKg))
            .ToArray();
        if (removed.Length == 0 && added.Length == 0)
        {
            return null;
        }

        var routeReallocations = reallocations
            .Where(item => item.SourceRouteId == routeId || item.DestinationRouteId == routeId)
            .ToArray();
        var proposedOccupancy = routeReallocations.LastOrDefault(item => item.SourceRouteId == routeId)?.SourceOccupancyAfter
            ?? routeReallocations.LastOrDefault(item => item.DestinationRouteId == routeId)?.DestinationOccupancyAfter
            ?? currentOccupancy;
        var reasons = routeReallocations.SelectMany(item => item.Reasons).DistinctBy(item => item.Code).ToArray();
        var warnings = Deserialize<IReadOnlyList<string>>(scenario.WarningsJson);

        return new RouteOptimizationRouteProjectionDto(
            routeId,
            routeName,
            currentOccupancy,
            proposedOccupancy,
            currentCapacityKg,
            currentCapacityKg,
            added,
            removed,
            reasons,
            warnings);
    }

    private async Task<(Guid ImportId, long Version)> ResolveSnapshotAsync(
        Guid importId,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.RouteImports.AsNoTracking()
            .Where(item => item.Id == importId)
            .Select(item => new { item.Id, item.Version })
            .SingleAsync(cancellationToken);
        return (snapshot.Id, snapshot.Version);
    }

    private async Task<(Guid ImportId, long Version)> ResolveSnapshotAtDateAsync(
        DateOnly referenceDate,
        CancellationToken cancellationToken)
    {
        var exclusiveEnd = RouteSnapshotDatePolicy.GetExclusiveUtcEnd(referenceDate);
        var importId = await dbContext.RouteImports.AsNoTracking()
            .Where(routeImport =>
                routeImport.DataSource!.Code == RouteImportCodes.DataSource &&
                routeImport.Status == RouteImportStatus.Completed &&
                routeImport.FinishedAt.HasValue &&
                routeImport.FinishedAt.Value < exclusiveEnd)
            .OrderByDescending(routeImport => routeImport.FinishedAt)
            .ThenByDescending(routeImport => routeImport.Version)
            .Select(routeImport => (Guid?)routeImport.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (importId is null)
        {
            throw new InvalidOperationException("Não há snapshot publicado para a data informada.");
        }

        return await ResolveSnapshotAsync(importId.Value, cancellationToken);
    }
}

public sealed class RouteOptimizationProcessingService(
    ImportDbContext dbContext,
    IEnumerable<IRouteOptimizationSolver> solvers,
    ILogger<RouteOptimizationProcessingService> logger) : IRouteOptimizationProcessingService
{
    private const int MaximumMovedCities = 2;
    private const int MaximumCandidateRoutes = 8;
    private const decimal MinimumOccupancyImprovement = 0.05m;
    private const decimal MaximumDestinationOccupancy = 1.00m;
    private const decimal MaximumEstimatedInsertionDistanceKm = 80m;
    private const int InputHashLength = 32;

    public async Task ProcessAsync(Guid optimizationRunId, CancellationToken cancellationToken)
    {
        var job = await dbContext.JobExecutions
            .SingleOrDefaultAsync(item =>
                item.JobType == RouteOptimizationCodes.JobType &&
                item.RelatedEntityId == optimizationRunId &&
                item.Status != JobExecutionStatus.Completed &&
                item.Status != JobExecutionStatus.Failed,
                cancellationToken);
        var run = await dbContext.RouteOptimizationRuns
            .SingleAsync(item => item.Id == optimizationRunId, cancellationToken);

        if (run.Status is RouteOptimizationStatus.Completed or
            RouteOptimizationStatus.NoChangeRecommended or
            RouteOptimizationStatus.InsufficientData or
            RouteOptimizationStatus.NoFeasibleSolution or
            RouteOptimizationStatus.Failed or
            RouteOptimizationStatus.Cancelled)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (job is not null)
        {
            job.Attempts++;
            job.StartedAt ??= now;
            job.Status = JobExecutionStatus.Processing;
            job.ErrorMessage = null;
        }

        run.Status = RouteOptimizationStatus.LoadingData;
        run.ProgressStage = RouteOptimizationStatus.LoadingData;
        run.StartedAt ??= now;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var problem = await BuildProblemAsync(run, cancellationToken);
            run.Status = RouteOptimizationStatus.SearchingSolutions;
            run.ProgressStage = RouteOptimizationStatus.SearchingSolutions;
            run.InputHash = problem.InputHash;
            run.SnapshotImportId = problem.SnapshotImportId;
            run.SnapshotImportVersion = problem.SnapshotImportVersion;
            await dbContext.SaveChangesAsync(cancellationToken);

            var solver = solvers.SingleOrDefault(item => item.CanHandle(problem.Scope))
                ?? throw new InvalidOperationException($"Não há solver para o escopo {problem.Scope}.");
            var solution = await solver.SolveAsync(problem, cancellationToken);

            run.Status = RouteOptimizationStatus.PersistingResult;
            run.ProgressStage = RouteOptimizationStatus.PersistingResult;
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.RouteOptimizationScenarios.RemoveRange(
                dbContext.RouteOptimizationScenarios.Where(item => item.RunId == run.Id));
            var scenarioRank = 1;
            foreach (var scenario in solution.Scenarios.Take(3))
            {
                dbContext.RouteOptimizationScenarios.Add(new RouteOptimizationScenario
                {
                    Id = Guid.NewGuid(),
                    RunId = run.Id,
                    Rank = scenarioRank,
                    Score = scenario.Score,
                    ActionType = scenario.ActionType,
                    IsRecommended = scenarioRank == 1,
                    Confidence = scenario.Confidence,
                    EstimatedDistanceChangeKm = scenario.EstimatedDistanceChangeKm,
                    CurrentMetricsJson = RouteOptimizationService.Serialize(scenario.CurrentMetrics),
                    ProposedMetricsJson = RouteOptimizationService.Serialize(scenario.ProposedMetrics),
                    WarningsJson = RouteOptimizationService.Serialize(scenario.Warnings),
                    ReasonsJson = RouteOptimizationService.Serialize(scenario.Reasons),
                    CityReallocationsJson = RouteOptimizationService.Serialize(scenario.CityReallocations),
                    TruckChangeJson = scenario.TruckChange is null
                        ? null
                        : RouteOptimizationService.Serialize(scenario.TruckChange),
                    RouteSequencesJson = RouteOptimizationService.Serialize(scenario.RouteSequences ?? []),
                    CreatedAt = DateTime.UtcNow
                });
                scenarioRank++;
            }

            run.Status = solution.Status;
            run.ProgressStage = solution.Status;
            run.Confidence = solution.Confidence;
            run.CompletedAt = DateTime.UtcNow;
            run.ErrorCode = null;
            run.ErrorMessage = null;
            if (job is not null)
            {
                job.Status = JobExecutionStatus.Completed;
                job.FinishedAt = run.CompletedAt;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Falha ao processar otimização de rota {OptimizationRunId}.", optimizationRunId);
            dbContext.ChangeTracker.Clear();
            run = await dbContext.RouteOptimizationRuns.SingleAsync(item => item.Id == optimizationRunId, cancellationToken);
            var isDataIssue = IsControlledDataIssue(exception);
            run.Status = isDataIssue ? RouteOptimizationStatus.InsufficientData : RouteOptimizationStatus.Failed;
            run.ProgressStage = run.Status;
            run.Confidence = RouteOptimizationConfidence.Insufficient;
            run.CompletedAt = DateTime.UtcNow;
            run.ErrorCode = isDataIssue ? "InsufficientData" : "ProcessingFailed";
            run.ErrorMessage = isDataIssue
                ? exception.Message
                : "Não foi possível concluir a simulação de otimização.";

            job = await dbContext.JobExecutions
                .SingleOrDefaultAsync(item =>
                    item.JobType == RouteOptimizationCodes.JobType &&
                    item.RelatedEntityId == optimizationRunId,
                    cancellationToken);
            if (job is not null)
            {
                job.Status = isDataIssue ? JobExecutionStatus.Completed : JobExecutionStatus.Failed;
                job.ErrorMessage = exception.Message;
                job.FinishedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsControlledDataIssue(Exception exception) =>
        exception is InvalidOperationException &&
        (exception.Message.StartsWith("Não há snapshot", StringComparison.Ordinal) ||
         exception.Message.StartsWith("Rota selecionada", StringComparison.Ordinal));

    private async Task<RouteOptimizationProblem> BuildProblemAsync(
        RouteOptimizationRun run,
        CancellationToken cancellationToken)
    {
        run.ProgressStage = RouteOptimizationStatus.BuildingProblem;
        await dbContext.SaveChangesAsync(cancellationToken);

        var importId = run.SnapshotImportId ?? await ResolveImportAtDateAsync(run.ReferenceDate, cancellationToken);
        if (importId is null)
        {
            throw new InvalidOperationException("Não há snapshot publicado para a data informada.");
        }

        var snapshot = await dbContext.RouteImports.AsNoTracking()
            .Where(item => item.Id == importId.Value)
            .Select(item => new { item.Id, item.Version })
            .SingleAsync(cancellationToken);

        var routes = await dbContext.Routes.AsNoTracking()
            .Where(route => route.ImportId == snapshot.Id)
            .Include(route => route.VehicleType)
            .Include(route => route.Entries)
                .ThenInclude(entry => entry.Municipality)
                    .ThenInclude(municipality => municipality!.Coordinate)
            .Include(route => route.CustomerAssignments)
                .ThenInclude(assignment => assignment.Municipality)
                    .ThenInclude(municipality => municipality!.Coordinate)
            .OrderBy(route => route.Weekday)
            .ThenBy(route => route.Name)
            .ToListAsync(cancellationToken);

        if (run.TargetRouteId.HasValue && routes.All(route => route.Id != run.TargetRouteId.Value))
        {
            throw new InvalidOperationException("Rota selecionada não pertence ao snapshot da data informada.");
        }

        var truckModels = await dbContext.VehicleTypes.AsNoTracking()
            .Where(item => item.CapacityKg.HasValue && item.CapacityKg > 0)
            .OrderBy(item => item.CapacityKg)
            .ThenBy(item => item.Name)
            .Select(item => new OptimizationTruckModel(item.Id, item.Name, item.CapacityKg!.Value))
            .ToListAsync(cancellationToken);

        var optimizationRoutes = routes
            .Select(route =>
            {
                var assignedMunicipalitiesByName = route.CustomerAssignments
                    .Select(assignment => assignment.Municipality)
                    .Where(municipality => municipality is not null)
                    .Select(municipality => municipality!)
                    .GroupBy(municipality => municipality.NormalizedName)
                    .ToDictionary(group => group.Key, group => group.First());

                return new OptimizationRoute(
                    route.Id,
                    route.Name,
                    route.Weekday,
                    route.VehicleTypeId,
                    route.VehicleType?.Name ?? string.Empty,
                    route.VehicleType?.CapacityKg,
                    route.TotalWeightKg,
                    route.OverallOccupancy,
                    route.Entries
                        .OrderBy(entry => entry.Sequence)
                        .Select(entry =>
                        {
                            var municipality = entry.Municipality ??
                                (assignedMunicipalitiesByName.TryGetValue(
                                    MunicipalityNameNormalizer.Normalize(entry.Name),
                                    out var inferredMunicipality)
                                    ? inferredMunicipality
                                    : null);

                            return new OptimizationCity(
                                entry.Id,
                                entry.Name,
                                entry.AveragePerDay,
                                ToGeoPoint(municipality),
                                entry.Sequence);
                        })
                        .ToArray());
            })
            .ToArray();

        var inputHash = CalculateInputHash(run, optimizationRoutes, truckModels, snapshot.Id, snapshot.Version);
        return new RouteOptimizationProblem(
            run.Scope,
            run.ReferenceDate,
            run.TargetRouteId,
            snapshot.Id,
            snapshot.Version,
            optimizationRoutes,
            truckModels,
            new OptimizationConstraints(
                MaximumMovedCities,
                MaximumCandidateRoutes,
                MinimumOccupancyImprovement,
                MaximumDestinationOccupancy,
                MaximumEstimatedInsertionDistanceKm),
            inputHash);
    }

    private static GeoPoint? ToGeoPoint(Municipality? municipality) =>
        municipality?.Coordinate is null ||
        !municipality.Coordinate.Latitude.HasValue ||
        !municipality.Coordinate.Longitude.HasValue
            ? null
            : new GeoPoint(
                municipality.Coordinate.Latitude.Value,
                municipality.Coordinate.Longitude.Value);

    private async Task<Guid?> ResolveImportAtDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var exclusiveEnd = RouteSnapshotDatePolicy.GetExclusiveUtcEnd(date);

        return await dbContext.RouteImports.AsNoTracking()
            .Where(routeImport =>
                routeImport.DataSource!.Code == RouteImportCodes.DataSource &&
                routeImport.Status == RouteImportStatus.Completed &&
                routeImport.FinishedAt.HasValue &&
                routeImport.FinishedAt.Value < exclusiveEnd)
            .OrderByDescending(routeImport => routeImport.FinishedAt)
            .ThenByDescending(routeImport => routeImport.Version)
            .Select(routeImport => (Guid?)routeImport.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string CalculateInputHash(
        RouteOptimizationRun run,
        IReadOnlyList<OptimizationRoute> routes,
        IReadOnlyList<OptimizationTruckModel> truckModels,
        Guid snapshotImportId,
        long snapshotVersion)
    {
        var payload = RouteOptimizationService.Serialize(new
        {
            run.Scope,
            run.ReferenceDate,
            run.TargetRouteId,
            snapshotImportId,
            snapshotVersion,
            RouteOptimizationCodes.AlgorithmVersion,
            RouteOptimizationCodes.RulesVersion,
            routes,
            truckModels
        });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..InputHashLength];
    }
}
