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
        var refreshApproximate = false;
        if (jobId.HasValue)
        {
            job = await db.JobExecutions.SingleAsync(x => x.Id == jobId.Value, cancellationToken);
            using var document = JsonDocument.Parse(job.ParametersJson);
            statusFilter = CustomerRegistrationAddressCustomerStatuses.Read(document.RootElement);
            reprocessFailed = ReadReprocessFailed(document.RootElement);
            maximumRequests = ReadMaximumRequests(document.RootElement);
            refreshApproximate = ReadRefreshApproximate(document.RootElement);
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

        var processed = 0; var skippedResolved = 0; var refreshedApproximate = 0; var retainedApproximate = 0; var providerRequests = 0;
        var resolved = 0; var cached = 0; var notFound = 0; var failed = 0; var pending = 0;
        var exactCoordinates = 0; var interpolatedCoordinates = 0; var streetCoordinates = 0;
        var postalCodeCoordinates = 0; var municipalityCoordinates = 0;
        var insufficientData = 0; var invalidOrIncompatiblePostalCode = 0;
        var incompatibleMunicipalityOrState = 0; var exhaustedFallbacks = 0; var providerFailures = 0;
        var batchSize = Math.Max(1, options.Value.PersistenceBatchSize);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (jobId.HasValue && await db.JobExecutions.AsNoTracking().AnyAsync(x => x.Id == jobId && x.CancellationRequestedAt != null, cancellationToken))
                break;
            var current = candidate.Coordinate;
            var shouldRefreshApproximate = refreshApproximate &&
                current?.Status == CustomerAddressCoordinateStatuses.Resolved &&
                MatchLevelFromSource(current.Source) != AddressCoordinateMatchLevels.Exact &&
                !string.IsNullOrWhiteSpace(candidate.Address.Street);
            if ((current?.Status == CustomerAddressCoordinateStatuses.Resolved && !shouldRefreshApproximate) ||
                (!reprocessFailed && current is not null && current.Status != CustomerAddressCoordinateStatuses.Resolved))
            {
                processed++;
                skippedResolved++;
                await SaveIfBatchAsync();
                continue;
            }

            var normalized = NormalizeAddress(candidate.Address);
            var cachedCoordinate = shouldRefreshApproximate ? null : await db.CustomerAddressCoordinates.AsNoTracking()
                .FirstOrDefaultAsync(x => x.NormalizedAddress == normalized && x.Status == CustomerAddressCoordinateStatuses.Resolved,
                    cancellationToken);
            AddressCoordinateLookup lookup;
            if (cachedCoordinate is not null)
            {
                lookup = new(CustomerAddressCoordinateStatuses.Resolved, cachedCoordinate.Latitude, cachedCoordinate.Longitude,
                    cachedCoordinate.ProviderPlaceId, cachedCoordinate.DisplayName, cachedCoordinate.FailureReason,
                    MatchLevelFromSource(cachedCoordinate.Source),
                    cachedCoordinate.Source);
                cached++;
            }
            else
            {
                if (maximumRequests.HasValue && providerRequests >= maximumRequests.Value) break;
                try
                {
                    providerRequests++;
                    lookup = await provider.FindAsync(new(candidate.Address.StreetType, candidate.Address.Street ?? string.Empty, candidate.Address.Number,
                        candidate.Address.Neighborhood, candidate.Address.City ?? string.Empty, candidate.Address.StateCode ?? string.Empty,
                        candidate.Address.PostalCode), cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    pending++;
                    providerFailures++;
                    await SaveAsync();
                    throw;
                }
            }
            var now = DateTime.UtcNow;
            current ??= new CustomerAddressCoordinate { Id = Guid.NewGuid(), CustomerRegistrationAddressId = candidate.Address.Id, CreatedAt = now };
            if (shouldRefreshApproximate && lookup.Status != CustomerAddressCoordinateStatuses.Resolved)
            {
                processed++;
                retainedApproximate++;
                current.LastAttemptAt = now;
                current.UpdatedAt = now;
                await SaveIfBatchAsync();
                continue;
            }
            current.NormalizedAddress = normalized;
            current.Source = lookup.Source ?? (lookup.MatchLevel is null ? provider.SourceName : $"{provider.SourceName}_{lookup.MatchLevel}");
            current.Status = lookup.Status;
            current.Precision = lookup.MatchLevel ?? AddressCoordinateMatchLevels.Exact;
            current.Latitude = lookup.Latitude; current.Longitude = lookup.Longitude; current.ProviderPlaceId = lookup.PlaceId;
            current.DisplayName = lookup.DisplayName; current.FailureReason = lookup.FailureReason; current.LastAttemptAt = now;
            current.ResolvedAt = lookup.Status == CustomerAddressCoordinateStatuses.Resolved ? now : null; current.UpdatedAt = now;
            if (candidate.Coordinate is null) db.CustomerAddressCoordinates.Add(current);
            processed++;
            if (lookup.Status == CustomerAddressCoordinateStatuses.Resolved)
            {
                resolved++;
                if (shouldRefreshApproximate) refreshedApproximate++;
                switch (lookup.MatchLevel)
                {
                    case CustomerAddressCoordinatePrecisions.Interpolated: interpolatedCoordinates++; break;
                    case AddressCoordinateMatchLevels.Street: streetCoordinates++; break;
                    case AddressCoordinateMatchLevels.PostalCode: postalCodeCoordinates++; break;
                    case AddressCoordinateMatchLevels.Municipality: municipalityCoordinates++; break;
                    default: exactCoordinates++; break;
                }
            }
            else if (lookup.Status == CustomerAddressCoordinateStatuses.NotFound)
            {
                notFound++;
                CountDiagnostic(lookup.FailureReason);
            }
            else
            {
                failed++;
                providerFailures++;
            }
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
                job.ProgressMessage = $"Filtro {statusFilter}; {processed}/{candidates.Count}; {skippedResolved} ignorados; {providerRequests} consultas; {resolved} resolvidos; {refreshedApproximate} aproximações atualizadas; {retainedApproximate} aproximações preservadas ({exactCoordinates} exatos, {interpolatedCoordinates} interpolados, {streetCoordinates} por rua, {postalCodeCoordinates} por CEP, {municipalityCoordinates} por município); {cached} em cache; {notFound} não encontrados; {failed} falhas; {pending} pendentes";
                job.ResultJson = JsonSerializer.Serialize(new { customerStatus = statusFilter, reprocessFailed, refreshApproximate, maximumRequests, total = candidates.Count,
                    processed, skippedResolved, refreshedApproximate, retainedApproximate, providerRequests, resolved, exactCoordinates, interpolatedCoordinates, streetCoordinates, postalCodeCoordinates,
                    municipalityCoordinates, cached, notFound, failed, pending, diagnostics = new { insufficientData,
                        invalidOrIncompatiblePostalCode, incompatibleMunicipalityOrState, exhaustedFallbacks, providerFailures } });
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        void CountDiagnostic(string? reason)
        {
            if (reason?.StartsWith(AddressCoordinateFailureReasons.InsufficientData, StringComparison.Ordinal) == true) insufficientData++;
            else if (reason?.StartsWith(AddressCoordinateFailureReasons.InvalidOrIncompatiblePostalCode, StringComparison.Ordinal) == true) invalidOrIncompatiblePostalCode++;
            else if (reason?.StartsWith(AddressCoordinateFailureReasons.IncompatibleMunicipalityOrState, StringComparison.Ordinal) == true) incompatibleMunicipalityOrState++;
            else exhaustedFallbacks++;
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

    public static bool ReadRefreshApproximate(JsonElement parameters) =>
        parameters.TryGetProperty("refreshApproximate", out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false :
                throw new ArgumentException("$.refreshApproximate deve ser booleano.")
            : false;

    public static string? MatchLevelFromSource(string source) => source switch
    {
        "GOOGLE_GEOCODING" or "NOMINATIM" or "NOMINATIM_EXACT" or "GEOAPIFY_EXACT" => AddressCoordinateMatchLevels.Exact,
        "HERE_INTERPOLATED" => CustomerAddressCoordinatePrecisions.Interpolated,
        "NOMINATIM_STREET" or "GEOAPIFY_STREET" => AddressCoordinateMatchLevels.Street,
        "BRASIL_API_POSTAL_CODE" => AddressCoordinateMatchLevels.PostalCode,
        "NOMINATIM_MUNICIPALITY" or "GEOAPIFY_MUNICIPALITY" => AddressCoordinateMatchLevels.Municipality,
        _ => null
    };

    public static string NormalizeAddress(CustomerRegistrationAddress address)
    {
        var joined = string.Join("|", new[] { address.Street, address.Number, address.Neighborhood, address.City,
            address.StateCode, address.PostalCode }.Select(value => value?.Trim() ?? string.Empty));
        var decomposed = joined.Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
