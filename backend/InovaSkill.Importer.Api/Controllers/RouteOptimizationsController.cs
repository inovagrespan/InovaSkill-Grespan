using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/route-optimizations")]
public sealed class RouteOptimizationsController(
    ImportDbContext db,
    IJobExecutionLauncher launcher,
    IImportLifecycleService importLifecycle,
    IBackgroundJobDispatcher backgroundJobDispatcher,
    IMunicipalityCoordinateProvider municipalityProvider,
    IImportFileStorage importFileStorage,
    RoutesSpreadsheetParser spreadsheetParser) : ControllerBase
{
    private const int DefaultCandidatePageSize = 20;
    private const int MaximumCandidatePageSize = 50;
    private static readonly HashSet<string> SupportedWeekdays =
    ["MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY"];

    [HttpPost("simulate")]
    public async Task<ActionResult> Simulate(CancellationToken cancellationToken)
    {
        try
        {
            var parametersJson = "{}";
            if (HttpContext?.Request.ContentLength is > 0)
            {
                var request = await HttpContext.Request.ReadFromJsonAsync<SimulateRouteOptimizationRequest>(
                    cancellationToken: cancellationToken);
                if (request?.Weekdays is not null)
                {
                    var weekdays = NormalizeWeekdays(request.Weekdays);
                    parametersJson = JsonSerializer.Serialize(new { weekdays });
                }
            }
            var result = await launcher.LaunchAsync(new JobLaunchRequest(
                OperationalJobCodes.DailyRouteOptimization,
                OperationalJobCatalog.DailyRouteOptimization.ContractVersion,
                parametersJson,
                JobExecutionTrigger.Manual,
                ReadUserId()), cancellationToken);
            return Accepted(new { result.JobExecutionId, result.Status });
        }
        catch (ArgumentException exception) when (exception.Message.StartsWith("Já existe uma execução", StringComparison.Ordinal))
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] DateOnly? date, [FromQuery] string? weekday,
        CancellationToken cancellationToken)
    {
        var currentImportId = await ResolveCurrentImport(cancellationToken);
        var importId = date.HasValue ? await ResolveImport(date, cancellationToken) : currentImportId;
        if (importId is null) return Ok(new
        {
            snapshotId = (Guid?)null,
            isCurrentSnapshot = false,
            latestExecution = (object?)null,
            items = Array.Empty<object>()
        });
        var normalizedWeekday = weekday?.Trim().ToUpperInvariant();
        var query = db.DailyRouteOptimizationResults.AsNoTracking()
            .Where(result => result.RouteImportId == importId);
        if (!string.IsNullOrWhiteSpace(normalizedWeekday)) query = query.Where(result => result.Weekday == normalizedWeekday);
        var items = await query.OrderBy(result => result.Weekday).Select(result => new
        {
            result.Id, result.RouteImportId, result.Weekday, result.Status, result.Reason,
            result.CurrentDistanceMeters, result.CurrentDurationSeconds,
            result.ProposedDistanceMeters, result.ProposedDurationSeconds,
            result.CurrentVehicleCount,
            proposedVehicleCount = result.Status == DailyRouteOptimizationStatuses.Optimized
                ? result.Vehicles.Count(vehicle => !vehicle.IsIdle)
                : result.ProposedVehicleCount,
            additionalVehicleCount = result.Status == DailyRouteOptimizationStatuses.Optimized
                ? result.Vehicles.Count(vehicle => vehicle.IsAdditional && !vehicle.IsIdle)
                : result.AdditionalVehicleCount,
            additionalCapacityKg = result.Status == DailyRouteOptimizationStatuses.Optimized
                ? result.Vehicles.Where(vehicle => vehicle.IsAdditional && !vehicle.IsIdle).Sum(vehicle => vehicle.CapacityKg)
                : result.AdditionalCapacityKg,
            result.TotalWeightKg,
            issueCount = result.Issues.Count,
            result.InheritedFromResultId,
            isInherited = result.InheritedFromResultId != null,
            result.CreatedAt
        }).ToListAsync(cancellationToken);
        var latestJob = await db.JobExecutions.AsNoTracking()
            .Where(job => job.JobType == OperationalJobCodes.DailyRouteOptimization && job.RelatedEntityId == importId)
            .OrderByDescending(job => job.CreatedAt)
            .Select(job => new
            {
                job.Id, job.Status, job.ProgressPercent, job.ProgressMessage, job.ErrorMessage,
                job.CreatedAt, job.StartedAt, job.FinishedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
        var latestExecution = latestJob is null ? null : new
        {
            latestJob.Id,
            Status = latestJob.Status.ToString(),
            latestJob.ProgressPercent,
            latestJob.ProgressMessage,
            latestJob.ErrorMessage,
            latestJob.CreatedAt,
            latestJob.StartedAt,
            latestJob.FinishedAt
        };
        return Ok(new { snapshotId = importId, isCurrentSnapshot = currentImportId == importId, latestExecution, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await db.DailyRouteOptimizationResults.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id, item.RouteImportId, item.Weekday, item.Status, item.Reason,
                item.CurrentDistanceMeters, item.CurrentDurationSeconds,
                item.ProposedDistanceMeters, item.ProposedDurationSeconds,
                item.CurrentVehicleCount,
                proposedVehicleCount = item.Status == DailyRouteOptimizationStatuses.Optimized
                    ? item.Vehicles.Count(vehicle => !vehicle.IsIdle)
                    : item.ProposedVehicleCount,
                additionalVehicleCount = item.Status == DailyRouteOptimizationStatuses.Optimized
                    ? item.Vehicles.Count(vehicle => vehicle.IsAdditional && !vehicle.IsIdle)
                    : item.AdditionalVehicleCount,
                additionalCapacityKg = item.Status == DailyRouteOptimizationStatuses.Optimized
                    ? item.Vehicles.Where(vehicle => vehicle.IsAdditional && !vehicle.IsIdle).Sum(vehicle => vehicle.CapacityKg)
                    : item.AdditionalCapacityKg,
                item.TotalWeightKg,
                issueCount = item.Issues.Count,
                item.InheritedFromResultId,
                isInherited = item.InheritedFromResultId != null,
                item.CreatedAt,
                vehicles = item.Vehicles.OrderBy(vehicle => vehicle.Sequence).Select(vehicle => new
                {
                    vehicle.Id, vehicle.Sequence, vehicle.IsAdditional, vehicle.IsIdle,
                    vehicle.VehicleTypeId, vehicleType = vehicle.VehicleType!.Name,
                    vehicle.SourceRouteId, sourceRouteName = vehicle.SourceRoute != null ? vehicle.SourceRoute.Name : null,
                    vehicle.CapacityKg, vehicle.LoadKg, vehicle.Occupancy,
                    vehicle.DistanceMeters, vehicle.DurationSeconds,
                    stops = vehicle.Stops.OrderBy(stop => stop.Sequence).Select(stop => new
                    {
                        stop.Id, stop.Sequence, stop.MunicipalityId,
                        municipality = stop.Municipality!.Name, stop.WeightKg,
                        stop.DistanceFromPreviousMeters, stop.DurationFromPreviousSeconds
                    }).ToList()
                }).ToList()
            }).SingleOrDefaultAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{resultId:guid}/remediation")]
    public async Task<ActionResult> GetRemediation(Guid resultId, CancellationToken cancellationToken)
    {
        var result = await db.DailyRouteOptimizationResults.AsNoTracking()
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(item => item.Id == resultId, cancellationToken);
        if (result is null) return NotFound();
        var currentImportId = await ResolveCurrentImport(cancellationToken);
        var routes = await db.Routes.AsNoTracking()
            .Where(route => route.ImportId == result.RouteImportId && route.Weekday == result.Weekday)
            .Include(route => route.VehicleType)
            .Include(route => route.Entries).ThenInclude(entry => entry.Municipality).ThenInclude(item => item!.Coordinate)
            .OrderBy(route => route.Name).ToListAsync(cancellationToken);
        var readinessIssues = result.Issues.Count > 0
            ? result.Issues.Select(item => new DailyRouteOptimizationReadinessIssue(
                item.Code, item.RouteId, item.RouteEntryId, item.MunicipalityId, item.VehicleTypeId,
                item.Message, item.CurrentValue, item.CanResolve)).ToArray()
            : DailyRouteOptimizationReadinessEvaluator.Evaluate(routes);
        var legacyProvenance = await LocateLegacyProvenance(result.RouteImportId, routes, cancellationToken);
        var canResolve = currentImportId == result.RouteImportId && CanResolveRemediation();
        return Ok(new
        {
            resultId = result.Id,
            snapshotId = result.RouteImportId,
            expectedSnapshotId = result.RouteImportId,
            result.Weekday,
            isCurrentSnapshot = currentImportId == result.RouteImportId,
            readOnly = !canResolve,
            canResolve,
            issueCount = readinessIssues.Count,
            reason = result.Reason ?? DailyRouteOptimizationReadinessEvaluator.Summarize(readinessIssues),
            routes = routes.Select(route => new
            {
                routeId = route.Id,
                routeName = route.Name,
                sourceSheetName = route.SourceSheetName ?? legacyProvenance.GetValueOrDefault(route.Id)?.SheetName,
                sourceHeaderRowNumber = route.SourceHeaderRowNumber ?? legacyProvenance.GetValueOrDefault(route.Id)?.HeaderRowNumber,
                vehicleTypeId = route.VehicleTypeId,
                vehicleType = route.VehicleType!.Name,
                capacityKg = route.VehicleCapacityKgSnapshot > 0 ? route.VehicleCapacityKgSnapshot : (decimal?)null,
                issues = readinessIssues.Where(issue => issue.RouteId == route.Id),
                stops = route.Entries.OrderBy(entry => entry.Sequence).Select(entry => new
                {
                    routeEntryId = entry.Id,
                    entry.Sequence,
                    sourceRowNumber = entry.SourceRowNumber ?? legacyProvenance.GetValueOrDefault(route.Id)?.EntryRows.GetValueOrDefault(entry.Sequence),
                    entry.Name,
                    entry.MunicipalityId,
                    municipality = entry.Municipality?.Name,
                    weightKg = entry.AveragePerDay,
                    entry.IsExcludedFromOptimization,
                    issues = readinessIssues.Where(issue => issue.RouteEntryId == entry.Id)
                })
            })
        });
    }

    [HttpGet("municipality-candidates")]
    public async Task<ActionResult> MunicipalityCandidates(
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultCandidatePageSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return BadRequest(new { message = "Informe o município a pesquisar." });
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaximumCandidatePageSize);
        var result = await municipalityProvider.SearchAsync(query, page, pageSize, cancellationToken);
        return Ok(new
        {
            result.Page,
            result.PageSize,
            total = result.TotalCount,
            items = result.Items.Select(item => new
            {
                item.IbgeCode, item.Name, item.StateCode, item.Latitude, item.Longitude,
                source = municipalityProvider.SourceName
            })
        });
    }

    [HttpPost("{resultId:guid}/remediations")]
    public async Task<ActionResult> CreateRemediation(
        Guid resultId,
        [FromBody] CreateRouteOptimizationRemediationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Resolutions is null || request.Resolutions.Count == 0)
            return BadRequest(new { message = "Informe ao menos uma correção." });
        var userId = ReadUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await db.DailyRouteOptimizationResults.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == resultId, cancellationToken);
        if (result is null) return NotFound();
        var currentImportId = await ResolveCurrentImport(cancellationToken);
        if (request.ExpectedSnapshotId != result.RouteImportId || currentImportId != result.RouteImportId)
            return Conflict(new { message = "O snapshot foi atualizado. Reabra as pendências antes de salvar." });
        if (await db.RouteImports.AsNoTracking().AnyAsync(item => item.DerivedFromImportId == result.RouteImportId &&
                (item.Status == RouteImportStatus.Queued || item.Status == RouteImportStatus.Processing), cancellationToken))
            return Conflict(new { message = "Já existe uma correção deste snapshot na fila ou em processamento." });

        var sourceImport = await db.RouteImports.AsNoTracking()
            .SingleAsync(item => item.Id == result.RouteImportId, cancellationToken);
        var routes = await db.Routes
            .Where(route => route.ImportId == result.RouteImportId && route.Weekday == result.Weekday)
            .Include(route => route.VehicleType)
            .Include(route => route.Entries).ThenInclude(entry => entry.Municipality).ThenInclude(item => item!.Coordinate)
            .ToListAsync(cancellationToken);
        var issues = DailyRouteOptimizationReadinessEvaluator.Evaluate(routes);
        var entryById = routes.SelectMany(item => item.Entries).ToDictionary(item => item.Id);
        var prepared = new List<RouteImportCorrection>();
        var affectedWeekdays = new HashSet<string>(StringComparer.Ordinal);
        var weightCorrections = new HashSet<Guid>();
        var exclusions = new HashSet<Guid>();
        var municipalityLinks = new HashSet<Guid>();
        var coordinateCorrections = new HashSet<Guid>();
        var capacityCorrections = new HashSet<Guid>();
        var now = DateTime.UtcNow;

        foreach (var resolution in request.Resolutions)
        {
            var action = resolution.Action?.Trim().ToUpperInvariant();
            switch (action)
            {
                case RouteImportCorrectionKinds.SetWeight:
                {
                    if (!resolution.RouteEntryId.HasValue || !entryById.TryGetValue(resolution.RouteEntryId.Value, out var entry))
                        return BadRequest(new { message = "A parada indicada para correção de peso não pertence ao resultado." });
                    if (!issues.Any(issue => issue.Code == DailyRouteOptimizationIssueCodes.InvalidStopWeight && issue.RouteEntryId == entry.Id) ||
                        weightCorrections.Contains(entry.Id) || exclusions.Contains(entry.Id))
                        return BadRequest(new { message = "A correção de peso é duplicada ou não corresponde a uma pendência." });
                    decimal weight;
                    try { weight = ReadPositiveDecimal(resolution.WeightKg, "weightKg"); }
                    catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
                    var correction = NewCorrection(action, userId.Value, now, entry.RouteId, entry.Id);
                    correction.OriginalWeightKg = entry.AveragePerDay;
                    correction.CorrectedWeightKg = weight;
                    prepared.Add(correction);
                    weightCorrections.Add(entry.Id);
                    affectedWeekdays.Add(result.Weekday);
                    break;
                }
                case RouteImportCorrectionKinds.ExcludeStop:
                {
                    if (!resolution.RouteEntryId.HasValue || !entryById.TryGetValue(resolution.RouteEntryId.Value, out var entry))
                        return BadRequest(new { message = "A parada indicada para exclusão não pertence ao resultado." });
                    if (entry.AveragePerDay != 0)
                        return BadRequest(new { message = "Somente uma parada com peso exatamente zero pode ficar fora da simulação." });
                    if (!issues.Any(issue => issue.Code == DailyRouteOptimizationIssueCodes.InvalidStopWeight && issue.RouteEntryId == entry.Id) ||
                        exclusions.Contains(entry.Id) || weightCorrections.Contains(entry.Id) || municipalityLinks.Contains(entry.Id))
                        return BadRequest(new { message = "A exclusão é duplicada ou não corresponde a uma pendência de peso zero." });
                    var correction = NewCorrection(action, userId.Value, now, entry.RouteId, entry.Id);
                    correction.OriginalWeightKg = entry.AveragePerDay;
                    correction.ExcludeFromOptimization = true;
                    prepared.Add(correction);
                    exclusions.Add(entry.Id);
                    affectedWeekdays.Add(result.Weekday);
                    break;
                }
                case RouteImportCorrectionKinds.LinkMunicipality:
                {
                    if (!resolution.RouteEntryId.HasValue || !entryById.TryGetValue(resolution.RouteEntryId.Value, out var entry) ||
                        string.IsNullOrWhiteSpace(resolution.MunicipalityIbgeCode))
                        return BadRequest(new { message = "Informe a parada e o município oficial." });
                    if (!issues.Any(issue => issue.Code == DailyRouteOptimizationIssueCodes.MunicipalityNotLinked && issue.RouteEntryId == entry.Id) ||
                        municipalityLinks.Contains(entry.Id) || exclusions.Contains(entry.Id))
                        return BadRequest(new { message = "O vínculo municipal é duplicado ou não corresponde a uma pendência." });
                    var official = await municipalityProvider.FindByIbgeCodeAsync(resolution.MunicipalityIbgeCode, cancellationToken);
                    if (official is null) return BadRequest(new { message = "O município oficial informado não foi encontrado." });
                    var municipality = await EnsureOfficialMunicipality(official, now, cancellationToken);
                    var normalizedAlias = MunicipalityNameNormalizer.Normalize(entry.Name);
                    var alias = await db.MunicipalityAliases.AsNoTracking().SingleOrDefaultAsync(item =>
                        item.DataSourceId == sourceImport.DataSourceId && item.NormalizedAlias == normalizedAlias, cancellationToken);
                    if (alias is not null && alias.MunicipalityId != municipality.Id && !resolution.ConfirmAliasReplacement)
                        return Conflict(new { message = "Este nome já possui outro vínculo. Confirme explicitamente o remapeamento." });
                    var correction = NewCorrection(action, userId.Value, now, entry.RouteId, entry.Id);
                    correction.MunicipalityId = municipality.Id;
                    correction.OriginalMunicipalityId = alias?.MunicipalityId ?? entry.MunicipalityId;
                    correction.SourceLabel = entry.Name;
                    correction.OriginalLatitude = municipality.Coordinate?.Latitude;
                    correction.OriginalLongitude = municipality.Coordinate?.Longitude;
                    correction.CorrectedLatitude = official.Latitude;
                    correction.CorrectedLongitude = official.Longitude;
                    prepared.Add(correction);
                    municipalityLinks.Add(entry.Id);
                    var equivalentDays = await db.RouteEntries.AsNoTracking()
                        .Where(item => item.Route!.ImportId == result.RouteImportId)
                        .Select(item => new { item.Name, item.Route!.Weekday }).ToListAsync(cancellationToken);
                    foreach (var day in equivalentDays.Where(item =>
                                 MunicipalityNameNormalizer.Normalize(item.Name) == normalizedAlias).Select(item => item.Weekday))
                        affectedWeekdays.Add(day);
                    break;
                }
                case RouteImportCorrectionKinds.SetVehicleTypeCapacity:
                {
                    if (!resolution.VehicleTypeId.HasValue || !routes.Any(route => route.VehicleTypeId == resolution.VehicleTypeId))
                        return BadRequest(new { message = "O tipo de veículo indicado não pertence ao resultado." });
                    if (!issues.Any(issue => issue.Code == DailyRouteOptimizationIssueCodes.VehicleCapacityMissing &&
                                             issue.VehicleTypeId == resolution.VehicleTypeId) ||
                        capacityCorrections.Contains(resolution.VehicleTypeId.Value))
                        return BadRequest(new { message = "A capacidade é duplicada ou não corresponde a uma pendência." });
                    decimal capacity;
                    try { capacity = ReadPositiveDecimal(resolution.CapacityKg, "capacityKg"); }
                    catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
                    capacity = LogisticsVehicleCapacityPolicy.Normalize(capacity);
                    var vehicleType = routes.Select(route => route.VehicleType!).First(item => item.Id == resolution.VehicleTypeId);
                    var correction = NewCorrection(action, userId.Value, now);
                    correction.VehicleTypeId = vehicleType.Id;
                    correction.OriginalCapacityKg = routes.First(route => route.VehicleTypeId == vehicleType.Id).VehicleCapacityKgSnapshot;
                    correction.CorrectedCapacityKg = capacity;
                    prepared.Add(correction);
                    capacityCorrections.Add(vehicleType.Id);
                    foreach (var day in await db.Routes.AsNoTracking().Where(route => route.ImportId == result.RouteImportId &&
                                     route.VehicleTypeId == vehicleType.Id).Select(route => route.Weekday).Distinct().ToListAsync(cancellationToken))
                        affectedWeekdays.Add(day);
                    break;
                }
                case RouteImportCorrectionKinds.ConfirmOfficialCoordinate:
                case RouteImportCorrectionKinds.SetManualCoordinate:
                {
                    if (!resolution.MunicipalityId.HasValue)
                        return BadRequest(new { message = "Informe o município da coordenada." });
                    if (!issues.Any(issue => issue.Code == DailyRouteOptimizationIssueCodes.MunicipalityCoordinateMissing &&
                                             issue.MunicipalityId == resolution.MunicipalityId) ||
                        coordinateCorrections.Contains(resolution.MunicipalityId.Value))
                        return BadRequest(new { message = "A coordenada é duplicada ou não corresponde a uma pendência." });
                    decimal latitude;
                    decimal longitude;
                    if (action == RouteImportCorrectionKinds.ConfirmOfficialCoordinate)
                    {
                        var municipality = await db.Municipalities.AsNoTracking()
                            .SingleOrDefaultAsync(item => item.Id == resolution.MunicipalityId, cancellationToken);
                        if (municipality is null) return BadRequest(new { message = "O município informado não existe." });
                        var official = await municipalityProvider.ResolveAsync(municipality, cancellationToken);
                        if (official is null) return BadRequest(new { message = "A base oficial não possui coordenada única para o município." });
                        latitude = official.Latitude;
                        longitude = official.Longitude;
                    }
                    else
                    {
                        try
                        {
                            latitude = ReadDecimal(resolution.Latitude, "latitude");
                            longitude = ReadDecimal(resolution.Longitude, "longitude");
                        }
                        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
                        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
                            return BadRequest(new { message = "Latitude ou longitude fora dos limites permitidos." });
                    }
                    var currentCoordinate = await db.MunicipalityCoordinates.AsNoTracking()
                        .SingleOrDefaultAsync(item => item.MunicipalityId == resolution.MunicipalityId, cancellationToken);
                    var correction = NewCorrection(action, userId.Value, now);
                    correction.MunicipalityId = resolution.MunicipalityId;
                    correction.OriginalLatitude = currentCoordinate?.Latitude;
                    correction.OriginalLongitude = currentCoordinate?.Longitude;
                    correction.CorrectedLatitude = latitude;
                    correction.CorrectedLongitude = longitude;
                    prepared.Add(correction);
                    coordinateCorrections.Add(resolution.MunicipalityId.Value);
                    foreach (var day in await db.RouteEntries.AsNoTracking().Where(item => item.Route!.ImportId == result.RouteImportId &&
                                     item.MunicipalityId == resolution.MunicipalityId).Select(item => item.Route!.Weekday).Distinct().ToListAsync(cancellationToken))
                        affectedWeekdays.Add(day);
                    break;
                }
                default:
                    return BadRequest(new { message = $"A ação '{resolution.Action}' não é suportada." });
            }
        }

        var unresolved = issues.Where(issue => issue.Code switch
        {
            DailyRouteOptimizationIssueCodes.InvalidStopWeight => issue.RouteEntryId.HasValue &&
                !weightCorrections.Contains(issue.RouteEntryId.Value) && !exclusions.Contains(issue.RouteEntryId.Value),
            DailyRouteOptimizationIssueCodes.MunicipalityNotLinked => issue.RouteEntryId.HasValue &&
                !municipalityLinks.Contains(issue.RouteEntryId.Value) && !exclusions.Contains(issue.RouteEntryId.Value),
            DailyRouteOptimizationIssueCodes.MunicipalityCoordinateMissing => issue.MunicipalityId.HasValue &&
                !coordinateCorrections.Contains(issue.MunicipalityId.Value) &&
                (!issue.RouteEntryId.HasValue || !exclusions.Contains(issue.RouteEntryId.Value)),
            DailyRouteOptimizationIssueCodes.VehicleCapacityMissing => issue.VehicleTypeId.HasValue &&
                !capacityCorrections.Contains(issue.VehicleTypeId.Value),
            _ => true
        }).ToArray();
        if (unresolved.Length > 0)
            return UnprocessableEntity(new { message = "Ainda existem pendências bloqueantes.", issues = unresolved });

        await db.SaveChangesAsync(cancellationToken);
        var derivedImport = await importLifecycle.CreateAsync(
            sourceImport.DataSourceId, sourceImport.FileName, sourceImport.FilePath, cancellationToken);
        derivedImport.DerivedFromImportId = sourceImport.Id;
        derivedImport.CreatedByUserId = userId;
        foreach (var correction in prepared)
        {
            correction.DerivedImportId = derivedImport.Id;
            db.RouteImportCorrections.Add(correction);
        }
        foreach (var day in affectedWeekdays)
            db.RouteImportAffectedWeekdays.Add(new RouteImportAffectedWeekday { ImportId = derivedImport.Id, Weekday = day });
        var job = CreateProcessImportJob(derivedImport, userId.Value, now);
        db.JobExecutions.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            backgroundJobDispatcher.EnqueueImport(derivedImport.Id, job.Id);
        }
        catch (Exception exception)
        {
            job.Status = JobExecutionStatus.Failed;
            job.ErrorMessage = $"Falha ao enfileirar a correção: {exception.Message}";
            job.FinishedAt = DateTime.UtcNow;
            derivedImport.Status = RouteImportStatus.Failed;
            derivedImport.FailureMessage = "Não foi possível enfileirar a correção.";
            derivedImport.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
        return Accepted(new { derivedImportId = derivedImport.Id, jobExecutionId = job.Id, status = "QUEUED" });
    }

    [HttpGet("remediations/{derivedImportId:guid}")]
    public async Task<ActionResult> GetRemediationStatus(Guid derivedImportId, CancellationToken cancellationToken)
    {
        var remediation = await db.RouteImports.AsNoTracking()
            .Where(item => item.Id == derivedImportId && item.DerivedFromImportId != null)
            .Select(item => new
            {
                item.Id, item.DerivedFromImportId, item.Version, item.Status, item.FailureMessage,
                item.CreatedAt, item.FinishedAt,
                affectedWeekdays = item.AffectedWeekdays.OrderBy(day => day.Weekday).Select(day => day.Weekday),
                jobs = item.JobExecutions.OrderBy(job => job.CreatedAt).Select(job => new
                {
                    job.Id, job.JobType, job.Status, job.ProgressPercent, job.ProgressMessage,
                    job.ErrorMessage, job.ParentJobExecutionId, job.CreatedAt, job.FinishedAt
                })
            }).SingleOrDefaultAsync(cancellationToken);
        if (remediation is null) return NotFound();
        var currentImportId = await ResolveCurrentImport(cancellationToken);
        return Ok(new
        {
            derivedImportId = remediation.Id,
            remediation.DerivedFromImportId,
            remediation.Version,
            status = remediation.Status.ToString(),
            remediation.FailureMessage,
            remediation.CreatedAt,
            remediation.FinishedAt,
            isPublished = currentImportId == remediation.Id,
            remediation.affectedWeekdays,
            jobs = remediation.jobs.Select(job => new
            {
                job.Id, job.JobType, status = job.Status.ToString(), job.ProgressPercent,
                job.ProgressMessage, job.ErrorMessage, job.ParentJobExecutionId, job.CreatedAt, job.FinishedAt
            })
        });
    }

    [HttpPost("remediations/{derivedImportId:guid}/retry")]
    public async Task<ActionResult> RetryRemediation(Guid derivedImportId, CancellationToken cancellationToken)
    {
        var import = await db.RouteImports.Include(item => item.AffectedWeekdays)
            .SingleOrDefaultAsync(item => item.Id == derivedImportId && item.DerivedFromImportId != null, cancellationToken);
        if (import is null) return NotFound();
        if (await db.JobExecutions.AnyAsync(job => job.RelatedEntityId == derivedImportId &&
                (job.Status == JobExecutionStatus.Queued || job.Status == JobExecutionStatus.Processing ||
                 job.Status == JobExecutionStatus.Retrying), cancellationToken))
            return Conflict(new { message = "A correção já está na fila ou em processamento." });
        var now = DateTime.UtcNow;
        JobExecution job;
        var retryOptimizationOnly = false;
        var latestFailedJobId = await db.JobExecutions.AsNoTracking()
            .Where(item => item.RelatedEntityId == import.Id && item.Status == JobExecutionStatus.Failed)
            .OrderByDescending(item => item.CreatedAt).Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (import.Status == RouteImportStatus.Completed && await ResolveCurrentImport(cancellationToken) == import.Id)
        {
            retryOptimizationOnly = true;
            var parentProcessJobId = await db.JobExecutions.AsNoTracking()
                .Where(item => item.RelatedEntityId == import.Id && item.JobType == OperationalJobCodes.ProcessImport)
                .OrderByDescending(item => item.CreatedAt).Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            job = new JobExecution
            {
                Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization,
                ContractVersion = OperationalJobCatalog.DailyRouteOptimization.ContractVersion,
                Queue = BackgroundJobQueues.Default, Trigger = JobExecutionTrigger.Manual,
                ParametersJson = JsonSerializer.Serialize(new { weekdays = import.AffectedWeekdays.Select(item => item.Weekday) }),
                Status = JobExecutionStatus.Queued, RelatedEntityId = import.Id, RequestedByUserId = ReadUserId(),
                ParentJobExecutionId = parentProcessJobId, RetriedFromJobExecutionId = latestFailedJobId,
                ProgressMessage = "Na fila", CreatedAt = now
            };
            db.JobExecutions.Add(job);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (await ResolveCurrentImport(cancellationToken) != import.DerivedFromImportId)
                return Conflict(new { message = "O snapshot de origem não é mais o atual." });
            import.Status = RouteImportStatus.Queued;
            import.StartedAt = null;
            import.FinishedAt = null;
            import.FailureMessage = null;
            job = CreateProcessImportJob(import, ReadUserId(), now);
            job.RetriedFromJobExecutionId = latestFailedJobId;
            db.JobExecutions.Add(job);
            await db.SaveChangesAsync(cancellationToken);
        }
        try
        {
            if (retryOptimizationOnly) backgroundJobDispatcher.EnqueueOperationalJob(job.Id);
            else backgroundJobDispatcher.EnqueueImport(import.Id, job.Id);
        }
        catch (Exception exception)
        {
            job.Status = JobExecutionStatus.Failed;
            job.ErrorMessage = $"Falha ao enfileirar o retry: {exception.Message}";
            job.FinishedAt = DateTime.UtcNow;
            if (!retryOptimizationOnly)
            {
                import.Status = RouteImportStatus.Failed;
                import.FailureMessage = "Não foi possível enfileirar o retry da correção.";
                import.FinishedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(cancellationToken);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = job.ErrorMessage });
        }
        return Accepted(new { derivedImportId = import.Id, jobExecutionId = job.Id, status = "QUEUED" });
    }

    private async Task<Municipality> EnsureOfficialMunicipality(
        MunicipalityCoordinateLookup official,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var municipality = await db.Municipalities.Include(item => item.Coordinate)
            .SingleOrDefaultAsync(item => item.IbgeCode == official.IbgeCode, cancellationToken);
        if (municipality is null)
        {
            municipality = new Municipality
            {
                Id = Guid.NewGuid(), IbgeCode = official.IbgeCode, StateCode = official.StateCode,
                Name = official.Name, NormalizedName = official.NormalizedName, CreatedAt = now
            };
            db.Municipalities.Add(municipality);
        }
        return municipality;
    }

    private async Task<IReadOnlyDictionary<Guid, LegacyRouteProvenance>> LocateLegacyProvenance(
        Guid importId,
        IReadOnlyCollection<InovaSkill.Importer.Domain.Entities.Route> routes,
        CancellationToken cancellationToken)
    {
        if (importFileStorage is null || spreadsheetParser is null ||
            routes.All(route => route.SourceSheetName is not null && route.SourceHeaderRowNumber.HasValue &&
                                route.Entries.All(entry => entry.SourceRowNumber.HasValue)))
            return new Dictionary<Guid, LegacyRouteProvenance>();
        try
        {
            var import = await db.RouteImports.AsNoTracking().Include(item => item.Errors)
                .SingleAsync(item => item.Id == importId, cancellationToken);
            var corrections = import.Errors.Where(item => item.Status == ImportErrorStatus.Resolved && item.CorrectedValue != null)
                .Select(item => new SpreadsheetCorrection(item.SheetName, item.RowNumber, item.Field, item.CorrectedValue!))
                .ToArray();
            await using var content = await importFileStorage.OpenReadAsync(import.FilePath, cancellationToken);
            var parsed = spreadsheetParser.Parse(content, corrections);
            var available = parsed.Routes.ToList();
            var result = new Dictionary<Guid, LegacyRouteProvenance>();
            foreach (var route in routes)
            {
                var match = available.FirstOrDefault(item => item.Weekday == route.Weekday &&
                                                              string.Equals(item.Name, route.Name, StringComparison.OrdinalIgnoreCase));
                if (match is null) continue;
                available.Remove(match);
                result[route.Id] = new LegacyRouteProvenance(
                    match.SourceSheetName,
                    match.SourceHeaderRowNumber,
                    match.Entries.ToDictionary(item => item.Sequence, item => item.SourceRowNumber));
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new Dictionary<Guid, LegacyRouteProvenance>();
        }
    }

    private static RouteImportCorrection NewCorrection(
        string kind,
        long userId,
        DateTime now,
        Guid? sourceRouteId = null,
        Guid? sourceRouteEntryId = null) => new()
        {
            Id = Guid.NewGuid(), Kind = kind, SourceRouteId = sourceRouteId,
            SourceRouteEntryId = sourceRouteEntryId, RequestedByUserId = userId, CreatedAt = now
        };

    private static JobExecution CreateProcessImportJob(RouteImport import, long? userId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), JobType = OperationalJobCodes.ProcessImport,
        ContractVersion = OperationalJobCatalog.ProcessImport.ContractVersion,
        Queue = BackgroundJobQueues.Imports, Trigger = JobExecutionTrigger.Manual,
        ParametersJson = JsonSerializer.Serialize(new { importId = import.Id }),
        Status = JobExecutionStatus.Queued, RelatedEntityId = import.Id,
        RequestedByUserId = userId, ProgressMessage = "Na fila", CreatedAt = now
    };

    private bool CanResolveRemediation()
    {
        var role = HttpContext?.User.FindFirstValue(ClaimTypes.Role)?.Trim().ToLowerInvariant();
        return role is AppUserRoles.Diretor or AppUserRoles.Vendas or AppUserRoles.Logistica or AppUserRoles.Admin or AppUserRoles.AdminSystem;
    }

    private static decimal ReadPositiveDecimal(JsonElement? value, string field)
    {
        var parsed = ReadDecimal(value, field, preferPtBrGrouping: true);
        if (parsed <= 0) throw new ArgumentException($"{field} deve ser maior que zero.");
        return RouteLoadPolicy.Normalize(parsed);
    }

    private static decimal ReadDecimal(JsonElement? value, string field, bool preferPtBrGrouping = false)
    {
        if (!value.HasValue) throw new ArgumentException($"{field} é obrigatório.");
        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDecimal(out var numeric)) return numeric;
        if (value.Value.ValueKind == JsonValueKind.String)
        {
            var text = value.Value.GetString();
            var looksLikePtBrGrouping = preferPtBrGrouping && text is not null &&
                                        text.Split('.').Length > 1 && text.Split('.').Skip(1).All(group => group.Length == 3);
            if ((text?.Contains(',') == true || looksLikePtBrGrouping) && decimal.TryParse(text, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), out var ptBr)) return ptBr;
            if (decimal.TryParse(text, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var invariant)) return invariant;
            if (decimal.TryParse(text, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), out ptBr)) return ptBr;
        }
        throw new ArgumentException($"{field} deve ser um número válido.");
    }

    private static IReadOnlyList<string> NormalizeWeekdays(IReadOnlyList<string>? weekdays)
    {
        if (weekdays is null) return [];
        var normalized = weekdays.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        if (normalized.Length == 0) throw new ArgumentException("weekdays deve conter ao menos um dia.");
        if (normalized.Any(item => !SupportedWeekdays.Contains(item)))
            throw new ArgumentException("weekdays contém um dia inválido.");
        return normalized;
    }

    private async Task<Guid?> ResolveImport(DateOnly? date, CancellationToken cancellationToken)
    {
        if (!date.HasValue) return await ResolveCurrentImport(cancellationToken);
        var end = RouteSnapshotDatePolicy.GetExclusiveUtcEnd(date.Value);
        return await db.RouteImports.AsNoTracking().Where(item => item.DataSource!.Code == RouteImportCodes.DataSource &&
                item.Status == RouteImportStatus.Completed && item.FinishedAt < end)
            .OrderByDescending(item => item.FinishedAt).ThenByDescending(item => item.Version)
            .Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
    }

    private Task<Guid?> ResolveCurrentImport(CancellationToken cancellationToken) =>
        db.DataSources.AsNoTracking().Where(source => source.Code == RouteImportCodes.DataSource)
            .Select(source => source.CurrentImportId).SingleOrDefaultAsync(cancellationToken);

    private long? ReadUserId()
    {
        var principal = HttpContext?.User;
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : null;
    }
}

internal sealed record LegacyRouteProvenance(
    string SheetName,
    int HeaderRowNumber,
    IReadOnlyDictionary<int, int> EntryRows);

public sealed record SimulateRouteOptimizationRequest(IReadOnlyList<string>? Weekdays);

public sealed record CreateRouteOptimizationRemediationRequest(
    Guid ExpectedSnapshotId,
    IReadOnlyList<RouteOptimizationResolutionRequest> Resolutions);

public sealed record RouteOptimizationResolutionRequest(
    string Action,
    Guid? RouteId,
    Guid? RouteEntryId,
    Guid? MunicipalityId,
    string? MunicipalityIbgeCode,
    Guid? VehicleTypeId,
    JsonElement? WeightKg,
    JsonElement? CapacityKg,
    JsonElement? Latitude,
    JsonElement? Longitude,
    bool ConfirmAliasReplacement = false);
