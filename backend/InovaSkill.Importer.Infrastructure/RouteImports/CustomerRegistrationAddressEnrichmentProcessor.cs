using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class CustomerRegistrationAddressEnrichmentProcessor(
    ImportDbContext dbContext,
    ICustomerRegistrationAddressProvider addressProvider,
    IOptions<BrasilApiOptions> options) : IProgressReportingOperationalJobProcessor
{
    private const decimal MaximumProcessingProgressPercent = 99m;
    public const string SourceName = "BRASIL_API_CNPJ";
    public string JobType => OperationalJobCodes.CustomerRegistrationAddressEnrichment;

    public Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken) =>
        ProcessAsync(relatedEntityId, null, cancellationToken);

    public Task ProcessAsync(
        Guid relatedEntityId, Guid jobExecutionId, CancellationToken cancellationToken) =>
        ProcessAsync(relatedEntityId, (Guid?)jobExecutionId, cancellationToken);

    private async Task ProcessAsync(
        Guid relatedEntityId, Guid? jobExecutionId, CancellationToken cancellationToken)
    {
        JobExecution? job = null;
        var customerStatus = CustomerRegistrationAddressCustomerStatuses.Active;
        var refreshResolved = false;
        var refreshMissingNumber = false;
        var refreshIncomplete = false;
        if (jobExecutionId.HasValue)
        {
            job = await dbContext.JobExecutions.SingleAsync(
                item => item.Id == jobExecutionId.Value, cancellationToken);
            using var parameters = JsonDocument.Parse(job.ParametersJson);
            customerStatus = CustomerRegistrationAddressCustomerStatuses.Read(parameters.RootElement);
            refreshResolved = ReadRefreshResolved(parameters.RootElement);
            refreshMissingNumber = ReadRefreshMissingNumber(parameters.RootElement);
            refreshIncomplete = ReadRefreshIncomplete(parameters.RootElement);
        }

        var candidatesQuery = dbContext.CustomerSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == relatedEntityId && snapshot.DocumentType == "CNPJ")
            .AsQueryable();
        candidatesQuery = customerStatus switch
        {
            CustomerRegistrationAddressCustomerStatuses.Active =>
                candidatesQuery.Where(snapshot => snapshot.Customer!.IsActive),
            CustomerRegistrationAddressCustomerStatuses.Inactive =>
                candidatesQuery.Where(snapshot => !snapshot.Customer!.IsActive),
            CustomerRegistrationAddressCustomerStatuses.All => candidatesQuery,
            _ => throw new InvalidOperationException($"Filtro de clientes não suportado: {customerStatus}.")
        };
        var candidates = await candidatesQuery
            .Select(snapshot => new
            {
                snapshot.CustomerId,
                snapshot.DocumentNumber,
            })
            .OrderBy(snapshot => snapshot.CustomerId)
            .ToListAsync(cancellationToken);

        var customerIds = candidates.Select(candidate => candidate.CustomerId).ToArray();
        var existing = await dbContext.CustomerRegistrationAddresses
            .Where(address => customerIds.Contains(address.CustomerId))
            .ToDictionaryAsync(address => address.CustomerId, cancellationToken);

        var batchSize = Math.Max(1, options.Value.PersistenceBatchSize);
        var checkpoint = ReadCheckpoint(
            job?.ResultJson, customerStatus, refreshResolved, refreshMissingNumber, refreshIncomplete, candidates.Count);
        var processed = checkpoint.Processed;
        var resolved = checkpoint.Resolved;
        var invalid = checkpoint.Invalid;
        var notFound = checkpoint.NotFound;
        var pending = checkpoint.Pending;
        var complete = checkpoint.Complete;
        var withoutNumber = checkpoint.WithoutNumber;
        var postalOnly = checkpoint.PostalOnly;
        var complementedByPostalCode = checkpoint.ComplementedByPostalCode;
        var postalCodeNotFound = checkpoint.PostalCodeNotFound;
        var postalCodeIncompatible = checkpoint.PostalCodeIncompatible;
        var providerFailures = checkpoint.ProviderFailures;
        foreach (var candidate in candidates.Skip(processed))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (existing.TryGetValue(candidate.CustomerId, out var current) &&
                current.Status != CustomerRegistrationAddressStatuses.Failed &&
                !(refreshMissingNumber && current.Status == CustomerRegistrationAddressStatuses.Resolved &&
                    string.IsNullOrWhiteSpace(current.Number)) &&
                !(refreshIncomplete && current.Status == CustomerRegistrationAddressStatuses.Resolved &&
                    (string.IsNullOrWhiteSpace(current.Street) ||
                     !current.Source.StartsWith("BRASIL_API", StringComparison.Ordinal))) &&
                !(refreshResolved && current.Status == CustomerRegistrationAddressStatuses.Resolved))
            {
                processed++;
                if (processed % batchSize == 0)
                    await SaveBatchAsync();
                continue;
            }

            var now = DateTime.UtcNow;
            var address = current ?? new CustomerRegistrationAddress
            {
                Id = Guid.NewGuid(),
                CustomerId = candidate.CustomerId,
                CreatedAt = now
            };
            CustomerRegistrationAddressLookup lookup;
            try
            {
                lookup = await addressProvider.FindByCnpjAsync(candidate.DocumentNumber, cancellationToken);
            }
            catch (BrasilApiRateLimitException)
            {
                processed++;
                pending++;
                if (processed % batchSize == 0)
                    await SaveBatchAsync();
                continue;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                address.DocumentNumber = candidate.DocumentNumber;
                address.Source = SourceName;
                address.Status = CustomerRegistrationAddressStatuses.Failed;
                address.FailureReason = exception.Message;
                address.LastAttemptAt = now;
                address.ResolvedAt = null;
                address.UpdatedAt = now;
                if (current is null)
                {
                    dbContext.CustomerRegistrationAddresses.Add(address);
                    existing.Add(candidate.CustomerId, address);
                }
                await dbContext.SaveChangesAsync(cancellationToken);
                throw;
            }
            address.DocumentNumber = candidate.DocumentNumber;
            address.Source = lookup.Source;
            address.Status = lookup.Status;
            address.PostalCode = lookup.PostalCode;
            address.StateCode = lookup.StateCode;
            address.City = lookup.City;
            address.Street = lookup.Street;
            address.StreetType = lookup.StreetType;
            address.Number = lookup.Number;
            address.Complement = lookup.Complement;
            address.Neighborhood = lookup.Neighborhood;
            address.FailureReason = lookup.FailureReason;
            address.LastAttemptAt = now;
            address.ResolvedAt = lookup.Status == CustomerRegistrationAddressStatuses.Resolved ? now : null;
            address.UpdatedAt = now;
            if (current is null)
            {
                dbContext.CustomerRegistrationAddresses.Add(address);
                existing.Add(candidate.CustomerId, address);
            }
            processed++;
            switch (lookup.Status)
            {
                case CustomerRegistrationAddressStatuses.Resolved:
                    resolved++;
                    switch (CustomerAddressCompleteness.From(lookup.Street, lookup.Number))
                    {
                        case CustomerAddressCompleteness.Complete: complete++; break;
                        case CustomerAddressCompleteness.WithoutNumber: withoutNumber++; break;
                        case CustomerAddressCompleteness.PostalOnly: postalOnly++; break;
                    }
                    switch (lookup.PostalCodeEnrichmentStatus)
                    {
                        case PostalCodeEnrichmentStatuses.Complemented: complementedByPostalCode++; break;
                        case PostalCodeEnrichmentStatuses.NotFound: postalCodeNotFound++; break;
                        case PostalCodeEnrichmentStatuses.Incompatible: postalCodeIncompatible++; break;
                        case PostalCodeEnrichmentStatuses.TechnicalFailure: providerFailures++; break;
                    }
                    break;
                case CustomerRegistrationAddressStatuses.InvalidDocument: invalid++; break;
                case CustomerRegistrationAddressStatuses.NotFound: notFound++; break;
            }
            if (processed % batchSize == 0)
                await SaveBatchAsync();
        }

        await SaveBatchAsync();

        async Task SaveBatchAsync()
        {
            if (job is not null)
            {
                job.ProgressPercent = CalculateProgressPercent(processed, candidates.Count);
                job.ProgressMessage = $"Filtro {customerStatus}; {processed}/{candidates.Count} CNPJs; {resolved} resolvidos; " +
                    $"{complete} completos; {withoutNumber} sem número; {postalOnly} somente CEP; " +
                    $"{invalid} inválidos; {notFound} não encontrados; {pending} pendentes";
                job.ResultJson = JsonSerializer.Serialize(new
                {
                    customerStatus,
                    refreshResolved,
                    refreshMissingNumber,
                    refreshIncomplete,
                    total = candidates.Count,
                    processed,
                    resolved,
                    invalid,
                    notFound,
                    pending,
                    complete,
                    withoutNumber,
                    postalOnly,
                    complementedByPostalCode,
                    postalCodeNotFound,
                    postalCodeIncompatible,
                    providerFailures
                });
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public static decimal CalculateProgressPercent(int processed, int total) =>
        total <= 0 ? MaximumProcessingProgressPercent :
        Math.Clamp(Math.Round((decimal)processed / total * MaximumProcessingProgressPercent, 1),
            0, MaximumProcessingProgressPercent);

    public static bool ReadRefreshResolved(JsonElement parameters) =>
        parameters.TryGetProperty("refreshResolved", out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false :
                throw new ArgumentException("$.refreshResolved deve ser booleano.")
            : false;

    public static bool ReadRefreshMissingNumber(JsonElement parameters) =>
        parameters.TryGetProperty("refreshMissingNumber", out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false :
                throw new ArgumentException("$.refreshMissingNumber deve ser booleano.")
            : false;

    public static bool ReadRefreshIncomplete(JsonElement parameters) =>
        parameters.TryGetProperty("refreshIncomplete", out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false :
                throw new ArgumentException("$.refreshIncomplete deve ser booleano.")
            : false;

    private static ProcessingCheckpoint ReadCheckpoint(
        string? resultJson, string customerStatus, bool refreshResolved,
        bool refreshMissingNumber, bool refreshIncomplete, int total)
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return ProcessingCheckpoint.Empty;

        try
        {
            var checkpoint = JsonSerializer.Deserialize<ProcessingCheckpoint>(resultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (checkpoint is null ||
                !string.Equals(checkpoint.CustomerStatus, customerStatus, StringComparison.OrdinalIgnoreCase) ||
                checkpoint.RefreshResolved != refreshResolved ||
                checkpoint.RefreshMissingNumber != refreshMissingNumber || checkpoint.Total != total ||
                checkpoint.RefreshIncomplete != refreshIncomplete ||
                checkpoint.Processed < 0 || checkpoint.Processed > total ||
                checkpoint.Resolved < 0 || checkpoint.Invalid < 0 ||
                checkpoint.NotFound < 0 || checkpoint.Pending < 0 || checkpoint.Complete < 0 ||
                checkpoint.WithoutNumber < 0 || checkpoint.PostalOnly < 0 ||
                checkpoint.ComplementedByPostalCode < 0 || checkpoint.PostalCodeNotFound < 0 ||
                checkpoint.PostalCodeIncompatible < 0 || checkpoint.ProviderFailures < 0)
                return ProcessingCheckpoint.Empty;

            return checkpoint;
        }
        catch (JsonException)
        {
            return ProcessingCheckpoint.Empty;
        }
    }

    private sealed record ProcessingCheckpoint(
        string CustomerStatus, bool RefreshResolved, bool RefreshMissingNumber, bool RefreshIncomplete,
        int Total, int Processed, int Resolved, int Invalid, int NotFound, int Pending,
        int Complete, int WithoutNumber, int PostalOnly, int ComplementedByPostalCode,
        int PostalCodeNotFound, int PostalCodeIncompatible, int ProviderFailures)
    {
        public static ProcessingCheckpoint Empty { get; } =
            new(string.Empty, false, false, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
