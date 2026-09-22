using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using InovaSkill.Importer.Api.Assistant;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/routes")]
public sealed class RoutesController(ImportDbContext dbContext, IRouteGeometryClient? routeGeometryClient = null) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private const int OccupancyPercentScale = 100;
    private const int OccupancyPercentDecimalPlaces = 1;
    private static readonly HashSet<string> SupportedOccupancyLevels =
    [
        "critical",
        "good",
        "medium",
        "idle",
        "unavailable"
    ];
    private static readonly HashSet<string> SupportedWeekdays =
    ["MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY"];
    private static readonly HashSet<string> RouteOptimizationDecisionRoles =
    [AppUserRoles.Vendas, AppUserRoles.Logistica, AppUserRoles.Admin, AppUserRoles.AdminSystem];
    private const int MaximumDecisionJustificationLength = 1000;
    private const int MaximumOptimizedRouteStops = 100;

    [HttpPost("daily-optimization")]
    public async Task<ActionResult> StartDailyOptimization(
        [FromBody] StartDailyOptimizationRequest request,
        [FromServices] IJobExecutionLauncher launcher,
        CancellationToken cancellationToken)
    {
        var weekday = request.Weekday?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(weekday) || !SupportedWeekdays.Contains(weekday))
            return BadRequest(new { message = "Dia inválido. Use MONDAY, TUESDAY, WEDNESDAY, THURSDAY ou FRIDAY." });
        if (!await dbContext.RouteImports.AnyAsync(item => item.Id == request.ImportId, cancellationToken))
            return NotFound(new { message = "O snapshot de rotas não foi encontrado." });
        try
        {
            var launched = await launcher.LaunchAsync(new JobLaunchRequest(
                OperationalJobCodes.DailyRouteOptimization,
                DailyRouteOptimizationPolicy.ContractVersion,
                JsonSerializer.Serialize(new { importId = request.ImportId, weekday }),
                JobExecutionTrigger.Manual), cancellationToken);
            return Accepted(new { jobExecutionId = launched.JobExecutionId, status = launched.Status });
        }
        catch (ArgumentException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet("daily-optimization")]
    public async Task<ActionResult> GetDailyOptimization(
        [FromQuery] Guid importId,
        [FromQuery] string weekday,
        CancellationToken cancellationToken)
    {
        var normalized = weekday?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized) || !SupportedWeekdays.Contains(normalized))
            return BadRequest(new { message = "Dia da semana inválido." });
        var jobs = await dbContext.JobExecutions.AsNoTracking()
            .Where(job => job.JobType == OperationalJobCodes.DailyRouteOptimization && job.RelatedEntityId == importId)
            .OrderByDescending(job => job.CreatedAt)
            .Select(job => new { job.Id, job.Status, job.ProgressPercent, job.ProgressMessage,
                job.ParametersJson, job.ResultJson, job.ErrorMessage, job.CreatedAt, job.FinishedAt })
            .ToListAsync(cancellationToken);
        var match = jobs.FirstOrDefault(job =>
        {
            using var document = JsonDocument.Parse(job.ParametersJson);
            return document.RootElement.TryGetProperty("weekday", out var value) &&
                string.Equals(value.GetString(), normalized, StringComparison.OrdinalIgnoreCase);
        });
        if (match is null) return NoContent();
        var decision = await dbContext.RouteOptimizationDecisions.AsNoTracking()
            .Where(item => item.JobExecutionId == match.Id)
            .Select(item => new RouteOptimizationDecisionResponse(
                item.Status,
                item.DecidedAt,
                item.Justification,
                item.DecidedByUserId,
                item.DecidedByUser == null ? null : item.DecidedByUser.Name))
            .SingleOrDefaultAsync(cancellationToken);
        return Ok(new
        {
            jobExecutionId = match.Id,
            status = match.Status.ToString(),
            match.ProgressPercent,
            match.ProgressMessage,
            result = match.ResultJson is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(match.ResultJson),
            match.ErrorMessage,
            match.CreatedAt,
            match.FinishedAt,
            decision = decision ?? new RouteOptimizationDecisionResponse(
                RouteOptimizationDecisionStatuses.Pending, null, null, null, null)
        });
    }

    [HttpPut("daily-optimization/{jobExecutionId:guid}/decision")]
    public async Task<ActionResult> DecideDailyOptimization(
        Guid jobExecutionId,
        [FromBody] DecideDailyOptimizationRequest request,
        CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? string.Empty;
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!RouteOptimizationDecisionRoles.Contains(role)) return Forbid();
        if (!long.TryParse(userIdValue, out var userId)) return Unauthorized();

        var justification = request.Justification?.Trim();
        if (justification?.Length > MaximumDecisionJustificationLength)
            return BadRequest(new { message = $"A justificativa deve ter no máximo {MaximumDecisionJustificationLength} caracteres." });
        var job = await dbContext.JobExecutions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == jobExecutionId && item.JobType == OperationalJobCodes.DailyRouteOptimization,
            cancellationToken);
        if (job is null) return NotFound(new { message = "A simulação não foi encontrada." });
        if (job.Status != JobExecutionStatus.Completed || string.IsNullOrWhiteSpace(job.ResultJson))
            return Conflict(new { message = "Somente uma simulação concluída pode receber uma decisão." });
        if (await dbContext.RouteOptimizationDecisions.AnyAsync(item => item.JobExecutionId == jobExecutionId, cancellationToken))
            return Conflict(new { message = "Esta simulação já possui uma decisão registrada." });

        var now = DateTime.UtcNow;
        var decision = new RouteOptimizationDecision
        {
            JobExecutionId = jobExecutionId,
            Status = request.Approved ? RouteOptimizationDecisionStatuses.Approved : RouteOptimizationDecisionStatuses.Rejected,
            DecidedByUserId = userId,
            DecidedAt = now,
            Justification = string.IsNullOrWhiteSpace(justification) ? null : justification,
            CreatedAt = now
        };
        dbContext.RouteOptimizationDecisions.Add(decision);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { decision.Status, decision.DecidedAt, decision.Justification, decidedByUserId = userId });
    }

    [HttpGet("occupancy-summary")]
    public async Task<ActionResult> GetOccupancySummary(CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.DataSources.AsNoTracking()
            .Where(source => source.Code == RouteImportCodes.DataSource)
            .Select(source => source.CurrentImportId == null
                ? null
                : new
                {
                    ImportId = source.CurrentImport!.Id,
                    source.CurrentImport.Version,
                    source.CurrentImport.FileName,
                    source.CurrentImport.FinishedAt
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return Ok(RouteOccupancySummaryResponse.Empty);
        }

        var routes = await dbContext.Routes.AsNoTracking()
            .Where(route => route.ImportId == snapshot.ImportId)
            .Select(route => new
            {
                route.TotalWeightKg,
                CapacityKg = route.VehicleType!.CapacityKg
            })
            .ToListAsync(cancellationToken);

        var routesWithCapacity = routes
            .Where(route => route.CapacityKg is > 0)
            .ToArray();
        var totalWeightKg = routesWithCapacity.Sum(route => route.TotalWeightKg);
        var totalCapacityKg = routesWithCapacity.Sum(route => route.CapacityKg!.Value);
        var occupancyRatePercent = totalCapacityKg > 0
            ? Math.Round(
                totalWeightKg / totalCapacityKg * OccupancyPercentScale,
                OccupancyPercentDecimalPlaces,
                MidpointRounding.AwayFromZero)
            : 0m;

        return Ok(new RouteOccupancySummaryResponse(
            occupancyRatePercent,
            totalWeightKg,
            totalCapacityKg,
            routes.Count,
            routesWithCapacity.Length,
            routes.Count - routesWithCapacity.Length,
            new RouteOccupancySnapshotResponse(
                snapshot.ImportId,
                snapshot.Version,
                snapshot.FileName,
                snapshot.FinishedAt)));
    }

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? weekday = null,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? date = null,
        [FromQuery] string? occupancyLevel = null,
        CancellationToken cancellationToken = default)
    {
        var importId = date.HasValue
            ? await ResolveImportAtDateAsync(date.Value, cancellationToken)
            : await dbContext.DataSources.AsNoTracking()
                .Where(source => source.Code == RouteImportCodes.DataSource)
                .Select(source => source.CurrentImportId)
                .SingleOrDefaultAsync(cancellationToken);

        return await ListByImportAsync(
            importId,
            page,
            pageSize,
            weekday,
            search,
            occupancyLevel,
            cancellationToken);
    }

    [HttpGet("/api/route-imports/{importId:guid}/routes")]
    public async Task<ActionResult> ListByImport(
        Guid importId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? weekday = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.RouteImports.AnyAsync(routeImport => routeImport.Id == importId, cancellationToken))
        {
            return NotFound();
        }

        return await ListByImportAsync(importId, page, pageSize, weekday, search, null, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var route = await dbContext.Routes.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Weekday,
                departureTime = item.DepartureTime,
                vehicleTypeId = item.VehicleTypeId,
                vehicleType = item.VehicleType!.Name,
                vehicleCapacityKg = item.VehicleCapacityKgSnapshot > 0 ? item.VehicleCapacityKgSnapshot : (decimal?)null,
                item.TotalWeightKg,
                item.TotalVolumeM3,
                item.TotalPallets,
                item.WeightOccupancy,
                item.VolumeOccupancy,
                item.PalletOccupancy,
                item.OverallOccupancy,
                occupancyStatus = item.OccupancyStatus.ToString(),
                importId = item.ImportId,
                importVersion = item.Import!.Version,
                importFileName = item.Import.FileName,
                item.CreatedAt,
                entries = item.Entries.OrderBy(entry => entry.Sequence).Select(entry => new
                {
                    entry.Id,
                    entry.Sequence,
                    entry.SourceRowNumber,
                    entry.Name,
                    entry.Deliveries,
                    entry.AveragePerDay,
                    entry.IsExcludedFromOptimization,
                    entry.Note
                }).ToList(),
                customers = item.CustomerAssignments
                    .Where(assignment => assignment.Customer != null)
                    .OrderBy(assignment => item.Entries
                        .Where(entry => entry.MunicipalityId == assignment.MunicipalityId)
                        .Select(entry => entry.Sequence)
                        .FirstOrDefault())
                    .ThenBy(assignment => assignment.Customer!.ExternalCode)
                    .Select(assignment => new
                    {
                        id = assignment.CustomerId,
                        code = assignment.Customer!.ExternalCode,
                        name = assignment.Customer.Snapshots
                            .OrderByDescending(snapshot => snapshot.CreatedAt)
                            .Select(snapshot => string.IsNullOrWhiteSpace(snapshot.TradeName)
                                ? snapshot.LegalName
                                : snapshot.TradeName)
                            .FirstOrDefault() ?? assignment.Customer.ExternalCode,
                        municipality = assignment.Municipality != null
                            ? assignment.Municipality.Name
                            : null,
                        address = assignment.Customer.RegistrationAddress == null
                            ? null
                            : new
                            {
                                assignment.Customer.RegistrationAddress.StreetType,
                                street = assignment.Customer.RegistrationAddress.Street,
                                number = assignment.Customer.RegistrationAddress.Number,
                                neighborhood = assignment.Customer.RegistrationAddress.Neighborhood,
                                city = assignment.Customer.RegistrationAddress.City,
                                stateCode = assignment.Customer.RegistrationAddress.StateCode
                            }
                    }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return route is null ? NotFound() : Ok(route);
    }

    [HttpGet("{id:guid}/road-path")]
    public async Task<ActionResult> GetRoadPath(Guid id, CancellationToken cancellationToken)
    {
        var route = await dbContext.Routes.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Name,
                MunicipalityOrder = item.Entries.OrderBy(entry => entry.Sequence)
                    .Select(entry => entry.MunicipalityId)
                    .ToArray()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (route is null) return NotFound();

        var depot = await dbContext.LogisticsDepots.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (depot is null)
            return Conflict(new { message = "Configure a Matriz Grespan antes de visualizar o trajeto." });

        var municipalityOrder = route.MunicipalityOrder
            .Select((municipalityId, index) => new { municipalityId, index })
            .Where(item => item.municipalityId.HasValue)
            .GroupBy(item => item.municipalityId!.Value)
            .ToDictionary(group => group.Key, group => group.Min(item => item.index));
        var customers = await dbContext.RouteCustomerAssignments.AsNoTracking()
            .Where(assignment => assignment.RouteId == id && assignment.Customer!.IsActive &&
                assignment.Customer.RegistrationAddress!.Coordinate!.Status == CustomerAddressCoordinateStatuses.Resolved &&
                assignment.Customer.RegistrationAddress.Coordinate.Precision == CustomerAddressCoordinatePrecisions.Exact &&
                !CustomerAddressCoordinateQuality.ApproximateSources.Contains(assignment.Customer.RegistrationAddress.Coordinate.Source) &&
                assignment.Customer.RegistrationAddress.Coordinate.Latitude != null &&
                assignment.Customer.RegistrationAddress.Coordinate.Longitude != null)
            .Select(assignment => new
            {
                assignment.CustomerId,
                assignment.MunicipalityId,
                assignment.Customer!.ExternalCode,
                Name = assignment.Customer.Snapshots.OrderByDescending(snapshot => snapshot.CreatedAt)
                    .Select(snapshot => string.IsNullOrEmpty(snapshot.TradeName) ? snapshot.LegalName : snapshot.TradeName)
                    .First(),
                Latitude = assignment.Customer.RegistrationAddress!.Coordinate!.Latitude!.Value,
                Longitude = assignment.Customer.RegistrationAddress.Coordinate.Longitude!.Value
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        if (customers.Count == 0)
            return Conflict(new { message = "Esta rota não possui clientes ativos com localização exata." });

        var orderedCustomers = customers
            .OrderBy(customer => customer.MunicipalityId.HasValue
                ? municipalityOrder.GetValueOrDefault(customer.MunicipalityId.Value, int.MaxValue)
                : int.MaxValue)
            .ThenBy(customer => customer.ExternalCode, StringComparer.Ordinal)
            .ToArray();
        if (orderedCustomers.All(customer =>
                customer.Latitude == depot.Latitude && customer.Longitude == depot.Longitude))
        {
            return Conflict(new
            {
                message = "Esta rota possui apenas clientes localizados na Matriz; não há percurso rodoviário para calcular."
            });
        }
        var points = new List<RouteGeometryPoint>(orderedCustomers.Length + 2)
        {
            new(depot.Id, depot.Name, depot.Latitude, depot.Longitude)
        };
        points.AddRange(orderedCustomers.Select(customer => new RouteGeometryPoint(
            customer.CustomerId,
            $"{customer.ExternalCode} · {customer.Name}",
            customer.Latitude,
            customer.Longitude)));
        points.Add(new RouteGeometryPoint(depot.Id, depot.Name, depot.Latitude, depot.Longitude));

        try
        {
            var client = routeGeometryClient ?? throw new InvalidOperationException("O provedor de geometria de rotas não foi configurado.");
            var result = await client.GetRouteAsync(points, cancellationToken);
            var customerMunicipalityById = orderedCustomers.ToDictionary(
                customer => customer.CustomerId,
                customer => customer.MunicipalityId);
            return Ok(new
            {
                route.Id,
                route.Name,
                result.Source,
                distanceMeters = result.DistanceMeters,
                durationSeconds = result.DurationSeconds,
                stops = result.Stops.Select((point, index) => new
                {
                    sequence = index,
                    point.Id,
                    point.Label,
                    point.Latitude,
                    point.Longitude,
                    isDepot = index == 0 || index == result.Stops.Count - 1,
                    municipalityId = customerMunicipalityById.TryGetValue(point.Id, out var municipalityId)
                        ? municipalityId
                        : (Guid?)null
                }),
                geometry = new
                {
                    type = "LineString",
                    coordinates = result.Geometry
                }
            });
        }
        catch (RouteGeometryException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
        }
    }

    [HttpPost("optimized-road-path")]
    public async Task<ActionResult> GetOptimizedRoadPath(
        [FromBody] OptimizedRoadPathRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MunicipalityIds is null || request.MunicipalityIds.Count == 0)
            return BadRequest(new { message = "Informe ao menos um município da rota otimizada." });
        if (request.MunicipalityIds.Count > MaximumOptimizedRouteStops)
            return BadRequest(new { message = $"A rota otimizada aceita no máximo {MaximumOptimizedRouteStops} paradas." });

        var depot = await dbContext.LogisticsDepots.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (depot is null)
            return Conflict(new { message = "Configure a Matriz Grespan antes de visualizar o trajeto." });

        if (!await dbContext.RouteImports.AsNoTracking().AnyAsync(item => item.Id == request.ImportId, cancellationToken))
            return NotFound(new { message = "O snapshot da rota otimizada não foi encontrado." });

        var municipalityOrder = request.MunicipalityIds
            .Select((municipalityId, index) => new { municipalityId, index })
            .GroupBy(item => item.municipalityId)
            .ToDictionary(group => group.Key, group => group.Min(item => item.index));
        var municipalityIds = municipalityOrder.Keys.ToArray();
        var customers = await dbContext.RouteCustomerAssignments.AsNoTracking()
            .Where(assignment => assignment.Route!.ImportId == request.ImportId &&
                assignment.MunicipalityId.HasValue && municipalityIds.Contains(assignment.MunicipalityId.Value) &&
                assignment.Customer!.IsActive &&
                assignment.Customer.RegistrationAddress!.Coordinate!.Status == CustomerAddressCoordinateStatuses.Resolved &&
                assignment.Customer.RegistrationAddress.Coordinate.Precision == CustomerAddressCoordinatePrecisions.Exact &&
                !CustomerAddressCoordinateQuality.ApproximateSources.Contains(assignment.Customer.RegistrationAddress.Coordinate.Source) &&
                assignment.Customer.RegistrationAddress.Coordinate.Latitude != null &&
                assignment.Customer.RegistrationAddress.Coordinate.Longitude != null)
            .Select(assignment => new
            {
                assignment.CustomerId,
                MunicipalityId = assignment.MunicipalityId!.Value,
                assignment.Customer!.ExternalCode,
                Name = assignment.Customer.Snapshots.OrderByDescending(snapshot => snapshot.CreatedAt)
                    .Select(snapshot => string.IsNullOrEmpty(snapshot.TradeName) ? snapshot.LegalName : snapshot.TradeName)
                    .First(),
                Latitude = assignment.Customer.RegistrationAddress!.Coordinate!.Latitude!.Value,
                Longitude = assignment.Customer.RegistrationAddress.Coordinate.Longitude!.Value
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        if (customers.Count == 0)
            return Conflict(new { message = "Esta rota otimizada não possui clientes ativos com localização exata." });

        var orderedCustomers = customers
            .OrderBy(customer => municipalityOrder[customer.MunicipalityId])
            .ThenBy(customer => customer.ExternalCode, StringComparer.Ordinal)
            .ToArray();
        var points = new List<RouteGeometryPoint>(orderedCustomers.Length + 2)
        {
            new(depot.Id, depot.Name, depot.Latitude, depot.Longitude)
        };
        points.AddRange(orderedCustomers.Select(customer => new RouteGeometryPoint(
            customer.CustomerId,
            $"{customer.ExternalCode} · {customer.Name}",
            customer.Latitude,
            customer.Longitude)));
        points.Add(new RouteGeometryPoint(depot.Id, depot.Name, depot.Latitude, depot.Longitude));

        try
        {
            var client = routeGeometryClient ?? throw new InvalidOperationException("O provedor de geometria de rotas não foi configurado.");
            var result = await client.GetRouteAsync(points, cancellationToken);
            var customerMunicipalityById = orderedCustomers.ToDictionary(
                customer => customer.CustomerId,
                customer => customer.MunicipalityId);
            return Ok(new
            {
                id = Guid.Empty,
                name = string.IsNullOrWhiteSpace(request.Name) ? "Rota otimizada" : request.Name.Trim(),
                result.Source,
                distanceMeters = result.DistanceMeters,
                durationSeconds = result.DurationSeconds,
                stops = result.Stops.Select((point, index) => new
                {
                    sequence = index,
                    point.Id,
                    point.Label,
                    point.Latitude,
                    point.Longitude,
                    isDepot = index == 0 || index == result.Stops.Count - 1,
                    municipalityId = customerMunicipalityById.TryGetValue(point.Id, out var municipalityId)
                        ? municipalityId
                        : (Guid?)null
                }),
                geometry = new { type = "LineString", coordinates = result.Geometry }
            });
        }
        catch (RouteGeometryException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    private async Task<ActionResult> ListByImportAsync(
        Guid? importId,
        int page,
        int pageSize,
        string? weekday,
        string? search,
        string? occupancyLevel,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var normalizedWeekday = weekday?.Trim().ToUpperInvariant();
        var normalizedOccupancyLevel = occupancyLevel?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(normalizedOccupancyLevel) &&
            !SupportedOccupancyLevels.Contains(normalizedOccupancyLevel))
        {
            return BadRequest(new
            {
                message = "Criticidade inválida. Use critical, good, medium, idle ou unavailable."
            });
        }

        if (!importId.HasValue)
        {
            return Ok(new { page, pageSize, total = 0, importId, items = Array.Empty<object>() });
        }

        var query = dbContext.Routes.AsNoTracking()
            .Where(route => route.ImportId == importId.Value);

        if (!string.IsNullOrWhiteSpace(normalizedWeekday))
        {
            query = query.Where(route => route.Weekday == normalizedWeekday);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = MunicipalityNameNormalizer.Normalize(search);
            query = query.Where(route =>
                route.Name.ToUpper().Contains(normalizedSearch) ||
                route.Entries.Any(entry => entry.Name.ToUpper().Contains(normalizedSearch)));
        }

        query = normalizedOccupancyLevel switch
        {
            null or "" => query,
            "critical" => query.Where(route =>
                route.OverallOccupancy > RouteOccupancyLevelPolicy.CriticalMinimumExclusive),
            "good" => query.Where(route =>
                route.OverallOccupancy >= RouteOccupancyLevelPolicy.GoodMinimum &&
                route.OverallOccupancy <= RouteOccupancyLevelPolicy.CriticalMinimumExclusive),
            "medium" => query.Where(route =>
                route.OverallOccupancy >= RouteOccupancyLevelPolicy.MediumMinimum &&
                route.OverallOccupancy < RouteOccupancyLevelPolicy.GoodMinimum),
            "idle" => query.Where(route =>
                route.OverallOccupancy != null &&
                route.OverallOccupancy < RouteOccupancyLevelPolicy.MediumMinimum),
            "unavailable" => query.Where(route => route.OverallOccupancy == null),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(route => route.Weekday)
            .ThenBy(route => route.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(route => new
            {
                route.Id,
                route.Name,
                route.Weekday,
                departureTime = route.DepartureTime,
                vehicleTypeId = route.VehicleTypeId,
                vehicleType = route.VehicleType!.Name,
                vehicleCapacityKg = route.VehicleCapacityKgSnapshot > 0 ? route.VehicleCapacityKgSnapshot : (decimal?)null,
                route.TotalWeightKg,
                route.TotalVolumeM3,
                route.TotalPallets,
                route.WeightOccupancy,
                route.VolumeOccupancy,
                route.PalletOccupancy,
                route.OverallOccupancy,
                occupancyStatus = route.OccupancyStatus.ToString(),
                importId = route.ImportId,
                importVersion = route.Import!.Version,
                importFileName = route.Import.FileName,
                entryCount = route.Entries.Count,
                totalDeliveries = route.Entries.Sum(entry => entry.Deliveries),
                route.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { page, pageSize, total, importId, items });
    }

    private async Task<Guid?> ResolveImportAtDateAsync(
        DateOnly date,
        CancellationToken cancellationToken)
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
}

public sealed record RouteOccupancySummaryResponse(
    decimal OccupancyRatePercent,
    decimal TotalWeightKg,
    decimal TotalCapacityKg,
    int RouteCount,
    int RoutesWithCapacity,
    int RoutesWithoutCapacity,
    RouteOccupancySnapshotResponse? Snapshot)
{
    public static RouteOccupancySummaryResponse Empty { get; } = new(0, 0, 0, 0, 0, 0, null);
}

public sealed record RouteOccupancySnapshotResponse(
    Guid ImportId,
    long Version,
    string FileName,
    DateTime? FinishedAt);

public sealed record StartDailyOptimizationRequest(Guid ImportId, string Weekday);
public sealed record OptimizedRoadPathRequest(Guid ImportId, string? Name, IReadOnlyList<Guid> MunicipalityIds);
public sealed record DecideDailyOptimizationRequest(bool Approved, string? Justification);
public sealed record RouteOptimizationDecisionResponse(
    string Status,
    DateTime? DecidedAt,
    string? Justification,
    long? DecidedByUserId,
    string? DecidedByUserName);
