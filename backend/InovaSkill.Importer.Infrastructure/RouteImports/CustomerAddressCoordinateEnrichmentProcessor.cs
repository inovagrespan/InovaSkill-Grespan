using System.Globalization;
using System.Text;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class CustomerAddressCoordinateEnrichmentProcessor(
    ImportDbContext db,
    ICustomerAddressCoordinateProvider provider,
    IOptions<NominatimOptions> options) : IProgressReportingOperationalJobProcessor
{
    private const decimal MaximumProcessingProgressPercent = 99m;
    private const int MaximumAllowedExternalRequests = 10000;
    public string JobType => OperationalJobCodes.CustomerAddressCoordinateEnrichment;

    public Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken) =>
        ProcessAsync(relatedEntityId, null, cancellationToken);
    public Task ProcessAsync(Guid relatedEntityId, Guid jobExecutionId, CancellationToken cancellationToken) =>
        ProcessAsync(relatedEntityId, (Guid?)jobExecutionId, cancellationToken);

    private async Task ProcessAsync(Guid importId, Guid? jobId, CancellationToken cancellationToken)
    {
        JobExecution? job = null;
        var statusFilter = CustomerRegistrationAddressCustomerStatuses.Active;
        var reprocessFailed = false;
        int? maximumRequests = null;
        if (jobId.HasValue)
        {
            job = await db.JobExecutions.SingleAsync(x => x.Id == jobId.Value, cancellationToken);
            using var document = JsonDocument.Parse(job.ParametersJson);
            statusFilter = CustomerRegistrationAddressCustomerStatuses.Read(document.RootElement);
            reprocessFailed = ReadReprocessFailed(document.RootElement);
            maximumRequests = ReadMaximumRequests(document.RootElement);
        }

        var query = db.CustomerSnapshots
            .Where(x => x.ImportId == importId && x.Customer!.RegistrationAddress!.Status == CustomerRegistrationAddressStatuses.Resolved);
        query = statusFilter switch
        {
            CustomerRegistrationAddressCustomerStatuses.Active => query.Where(x => x.Customer!.IsActive),
            CustomerRegistrationAddressCustomerStatuses.Inactive => query.Where(x => !x.Customer!.IsActive),
            _ => query
        };
        var candidates = await query.Select(x => new
        {
            Address = x.Customer!.RegistrationAddress!,
            Coordinate = x.Customer.RegistrationAddress!.Coordinate
        }).OrderBy(x => x.Coordinate != null && x.Coordinate.Source == "GOOGLE_GEOCODING" ? 1 : 0)
            .ThenBy(x => x.Address.Id).ToListAsync(cancellationToken);

        var processed = 0; var resolved = 0; var cached = 0; var notFound = 0; var failed = 0; var pending = 0;
        var externalRequests = 0;
        var batchSize = Math.Max(1, options.Value.PersistenceBatchSize);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (jobId.HasValue && await db.JobExecutions.AsNoTracking().AnyAsync(x => x.Id == jobId && x.CancellationRequestedAt != null, cancellationToken))
                break;
            var current = candidate.Coordinate;
            if (current?.Status == CustomerAddressCoordinateStatuses.Resolved ||
                (!reprocessFailed && current is not null)) { processed++; await SaveIfBatchAsync(); continue; }

            var normalized = NormalizeAddress(candidate.Address);
            var cachedCoordinate = await db.CustomerAddressCoordinates.AsNoTracking()
                .FirstOrDefaultAsync(x => x.NormalizedAddress == normalized && x.Status == CustomerAddressCoordinateStatuses.Resolved,
                    cancellationToken);
            AddressCoordinateLookup lookup;
            if (cachedCoordinate is not null)
            {
                lookup = new(CustomerAddressCoordinateStatuses.Resolved, cachedCoordinate.Latitude, cachedCoordinate.Longitude,
                    cachedCoordinate.ProviderPlaceId, cachedCoordinate.DisplayName, null);
                cached++;
            }
            else
            {
                if (maximumRequests.HasValue && externalRequests >= maximumRequests.Value) break;
                try
                {
                    externalRequests++;
                    lookup = await provider.FindAsync(new(candidate.Address.StreetType, candidate.Address.Street ?? string.Empty, candidate.Address.Number,
                        candidate.Address.Neighborhood, candidate.Address.City ?? string.Empty, candidate.Address.StateCode ?? string.Empty,
                        candidate.Address.PostalCode), cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    pending++;
                    await SaveAsync();
                    throw;
                }
            }
            var now = DateTime.UtcNow;
            current ??= new CustomerAddressCoordinate { Id = Guid.NewGuid(), CustomerRegistrationAddressId = candidate.Address.Id, CreatedAt = now };
            current.NormalizedAddress = normalized; current.Source = provider.SourceName; current.Status = lookup.Status;
            current.Latitude = lookup.Latitude; current.Longitude = lookup.Longitude; current.ProviderPlaceId = lookup.PlaceId;
            current.DisplayName = lookup.DisplayName; current.FailureReason = lookup.FailureReason; current.LastAttemptAt = now;
            current.ResolvedAt = lookup.Status == CustomerAddressCoordinateStatuses.Resolved ? now : null; current.UpdatedAt = now;
            if (candidate.Coordinate is null) db.CustomerAddressCoordinates.Add(current);
            processed++;
            if (lookup.Status == CustomerAddressCoordinateStatuses.Resolved) resolved++;
            else if (lookup.Status == CustomerAddressCoordinateStatuses.NotFound) notFound++;
            else failed++;
            await SaveIfBatchAsync();
        }
        await SaveAsync();

        async Task SaveIfBatchAsync() { if (processed % batchSize == 0) await SaveAsync(); }
        async Task SaveAsync()
        {
            if (job is not null)
            {
                job.ProgressPercent = candidates.Count == 0 ? MaximumProcessingProgressPercent :
                    Math.Clamp(Math.Round((decimal)processed / candidates.Count * MaximumProcessingProgressPercent, 1), 0, MaximumProcessingProgressPercent);
                job.ProgressMessage = $"Filtro {statusFilter}; {processed}/{candidates.Count}; {externalRequests} consultas externas; {resolved} resolvidos; {cached} em cache; {notFound} não encontrados; {failed} falhas; {pending} pendentes";
                job.ResultJson = JsonSerializer.Serialize(new { customerStatus = statusFilter, reprocessFailed, maximumRequests, total = candidates.Count, processed, externalRequests, resolved, cached, notFound, failed, pending });
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static bool ReadReprocessFailed(JsonElement parameters) =>
        parameters.TryGetProperty("reprocessFailed", out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false :
                throw new ArgumentException("$.reprocessFailed deve ser booleano.")
            : false;

    public static int? ReadMaximumRequests(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("maximumRequests", out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var maximumRequests) ||
            maximumRequests < 1 || maximumRequests > MaximumAllowedExternalRequests)
            throw new ArgumentException($"$.maximumRequests deve ser um inteiro entre 1 e {MaximumAllowedExternalRequests}.");
        return maximumRequests;
    }

    public static string NormalizeAddress(CustomerRegistrationAddress address)
    {
        var joined = string.Join("|", new[] { address.Street, address.Number, address.Neighborhood, address.City,
            address.StateCode, address.PostalCode }.Select(value => value?.Trim() ?? string.Empty));
        var decomposed = joined.Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
