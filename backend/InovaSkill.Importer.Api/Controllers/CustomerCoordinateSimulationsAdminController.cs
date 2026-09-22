using System.Security.Claims;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/admin/customer-coordinate-simulations")]
public sealed class CustomerCoordinateSimulationsAdminController(
    ImportDbContext db,
    IJobExecutionLauncher launcher) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var importId = await CurrentCustomerImportId(cancellationToken);
        if (!importId.HasValue) return Ok(EmptyResponse(page, pageSize));

        var baseQuery = db.CustomerSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == importId)
            .Include(snapshot => snapshot.Customer).ThenInclude(customer => customer!.RegistrationAddress)
                .ThenInclude(address => address!.Coordinate)
            .Include(snapshot => snapshot.Municipality).ThenInclude(municipality => municipality!.Coordinate);
        var exact = await baseQuery.CountAsync(snapshot =>
            snapshot.Customer!.RegistrationAddress!.Coordinate != null &&
            snapshot.Customer.RegistrationAddress.Coordinate.Status == CustomerAddressCoordinateStatuses.Resolved &&
            snapshot.Customer.RegistrationAddress.Coordinate.Precision == CustomerAddressCoordinatePrecisions.Exact &&
            !CustomerAddressCoordinateQuality.ApproximateSources.Contains(snapshot.Customer.RegistrationAddress.Coordinate.Source) &&
            snapshot.Customer.RegistrationAddress.Coordinate.Latitude != null &&
            snapshot.Customer.RegistrationAddress.Coordinate.Longitude != null, cancellationToken);
        var simulated = await baseQuery.CountAsync(snapshot =>
            snapshot.Customer!.RegistrationAddress!.Coordinate != null &&
            db.CustomerCoordinateSimulationAudits.Any(audit =>
                audit.CustomerRegistrationAddressId == snapshot.Customer.RegistrationAddress.Id &&
                audit.RevertedAt == null), cancellationToken);
        var total = await baseQuery.CountAsync(cancellationToken);

        IQueryable<CustomerSnapshot> query = baseQuery;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(snapshot =>
                EF.Functions.ILike(snapshot.Customer!.ExternalCode, pattern) ||
                EF.Functions.ILike(snapshot.LegalName, pattern) ||
                EF.Functions.ILike(snapshot.TradeName, pattern) ||
                EF.Functions.ILike(snapshot.Municipality!.Name, pattern));
        }
        var filteredTotal = await query.CountAsync(cancellationToken);
        var snapshots = await query.OrderBy(snapshot => snapshot.TradeName)
            .ThenBy(snapshot => snapshot.Customer!.ExternalCode)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var addressIds = snapshots.Where(item => item.Customer?.RegistrationAddress is not null)
            .Select(item => item.Customer!.RegistrationAddress!.Id).ToArray();
        var audits = await db.CustomerCoordinateSimulationAudits.AsNoTracking()
            .Where(audit => audit.CustomerRegistrationAddressId != null &&
                addressIds.Contains(audit.CustomerRegistrationAddressId.Value) && audit.RevertedAt == null)
            .ToDictionaryAsync(audit => audit.CustomerRegistrationAddressId!.Value, cancellationToken);
        var executions = await db.JobExecutions.AsNoTracking()
            .Where(job => job.JobType == OperationalJobCodes.CustomerCoordinateSimulation)
            .OrderByDescending(job => job.CreatedAt).Take(10)
            .Select(job => new
            {
                job.Id,
                status = job.Status.ToString(),
                job.ProgressPercent,
                job.ProgressMessage,
                job.ErrorMessage,
                job.CreatedAt,
                job.FinishedAt,
                canRevert = db.CustomerCoordinateSimulationAudits.Any(audit =>
                    audit.JobExecutionId == job.Id && audit.RevertedAt == null)
            }).ToListAsync(cancellationToken);

        return Ok(new
        {
            snapshotId = importId,
            summary = new { total, exact, simulated, pending = total - exact },
            page,
            pageSize,
            total = filteredTotal,
            items = snapshots.Select(snapshot =>
            {
                var address = snapshot.Customer?.RegistrationAddress;
                var coordinate = address?.Coordinate;
                audits.TryGetValue(address?.Id ?? Guid.Empty, out var audit);
                return new
                {
                    customerId = snapshot.CustomerId,
                    externalCode = snapshot.Customer?.ExternalCode,
                    name = string.IsNullOrWhiteSpace(snapshot.TradeName) ? snapshot.LegalName : snapshot.TradeName,
                    municipality = snapshot.Municipality?.Name,
                    stateCode = snapshot.Municipality?.StateCode,
                    registeredAddress = FormatAddress(address),
                    precision = coordinate?.Precision,
                    source = coordinate?.Source,
                    latitude = coordinate?.Latitude,
                    longitude = coordinate?.Longitude,
                    isSimulated = audit is not null,
                    baseLatitude = audit?.BaseLatitude,
                    baseLongitude = audit?.BaseLongitude,
                    baseSource = audit?.BaseSource,
                    simulatedAddress = audit?.DisplayName,
                    distanceMeters = audit?.DistanceMeters,
                    appliedAt = audit?.AppliedAt,
                    jobExecutionId = audit?.JobExecutionId
                };
            }),
            executions
        });
    }

    [HttpPost("run")]
    public async Task<ActionResult> Run(
        [FromBody] RunCustomerCoordinateSimulationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await launcher.LaunchAsync(new JobLaunchRequest(
                OperationalJobCodes.CustomerCoordinateSimulation,
                OperationalJobCatalog.CustomerCoordinateSimulation.ContractVersion,
                JsonSerializer.Serialize(new
                {
                    action = CustomerCoordinateSimulationProcessor.ApplyAction,
                    request.RecalculateDependents
                }),
                JobExecutionTrigger.Manual,
                ReadUserId()), cancellationToken);
            return Accepted(new { result.JobExecutionId, result.Status });
        }
        catch (ArgumentException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("revert")]
    public async Task<ActionResult> Revert(
        [FromBody] RevertCustomerCoordinateSimulationRequest request,
        CancellationToken cancellationToken)
    {
        if (!await db.CustomerCoordinateSimulationAudits.AsNoTracking().AnyAsync(audit =>
                audit.JobExecutionId == request.SourceJobExecutionId && audit.RevertedAt == null,
                cancellationToken))
            return Conflict(new { message = "A execução não possui coordenadas simuladas ativas para reverter." });
        try
        {
            var result = await launcher.LaunchAsync(new JobLaunchRequest(
                OperationalJobCodes.CustomerCoordinateSimulation,
                OperationalJobCatalog.CustomerCoordinateSimulation.ContractVersion,
                JsonSerializer.Serialize(new
                {
                    action = CustomerCoordinateSimulationProcessor.RevertAction,
                    sourceJobExecutionId = request.SourceJobExecutionId
                }),
                JobExecutionTrigger.Manual,
                ReadUserId()), cancellationToken);
            return Accepted(new { result.JobExecutionId, result.Status });
        }
        catch (ArgumentException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    private Task<Guid?> CurrentCustomerImportId(CancellationToken cancellationToken) =>
        db.DataSources.AsNoTracking().Where(source => source.Code == CustomerImportCodes.DataSource)
            .Select(source => source.CurrentImportId).SingleOrDefaultAsync(cancellationToken);

    private long? ReadUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : null;
    }

    private static string FormatAddress(CustomerRegistrationAddress? address) => address is null
        ? "Endereço cadastral indisponível"
        : string.Join(", ", new[]
        {
            address.StreetType, address.Street, address.Number, address.Neighborhood,
            address.City, address.StateCode, address.PostalCode
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static object EmptyResponse(int page, int pageSize) => new
    {
        snapshotId = (Guid?)null,
        summary = new { total = 0, exact = 0, simulated = 0, pending = 0 },
        page,
        pageSize,
        total = 0,
        items = Array.Empty<object>(),
        executions = Array.Empty<object>()
    };
}

public sealed record RevertCustomerCoordinateSimulationRequest(Guid SourceJobExecutionId);
public sealed record RunCustomerCoordinateSimulationRequest(bool RecalculateDependents);
