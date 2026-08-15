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
    public const string SourceName = "BRASIL_API";
    public string JobType => OperationalJobCodes.CustomerRegistrationAddressEnrichment;

    public Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken) =>
        ProcessAsync(relatedEntityId, null, cancellationToken);

    public Task ProcessAsync(
        Guid relatedEntityId, Guid jobExecutionId, CancellationToken cancellationToken) =>
        ProcessAsync(relatedEntityId, (Guid?)jobExecutionId, cancellationToken);

    private async Task ProcessAsync(
        Guid relatedEntityId, Guid? jobExecutionId, CancellationToken cancellationToken)
    {
        var candidates = await dbContext.CustomerSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.ImportId == relatedEntityId && snapshot.DocumentType == "CNPJ")
            .Select(snapshot => new { snapshot.CustomerId, snapshot.DocumentNumber })
            .OrderBy(snapshot => snapshot.CustomerId)
            .ToListAsync(cancellationToken);

        var customerIds = candidates.Select(candidate => candidate.CustomerId).ToArray();
        var existing = await dbContext.CustomerRegistrationAddresses
            .Where(address => customerIds.Contains(address.CustomerId))
            .ToDictionaryAsync(address => address.CustomerId, cancellationToken);

        var batchSize = Math.Max(1, options.Value.PersistenceBatchSize);
        var processed = 0;
        var resolved = 0;
        var invalid = 0;
        var notFound = 0;
        var pending = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (existing.TryGetValue(candidate.CustomerId, out var current) &&
                current.Status != CustomerRegistrationAddressStatuses.Failed)
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
            address.Source = SourceName;
            address.Status = lookup.Status;
            address.PostalCode = lookup.PostalCode;
            address.StateCode = lookup.StateCode;
            address.City = lookup.City;
            address.Street = lookup.Street;
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
                case CustomerRegistrationAddressStatuses.Resolved: resolved++; break;
                case CustomerRegistrationAddressStatuses.InvalidDocument: invalid++; break;
                case CustomerRegistrationAddressStatuses.NotFound: notFound++; break;
            }
            if (processed % batchSize == 0)
                await SaveBatchAsync();
        }

        await SaveBatchAsync();

        async Task SaveBatchAsync()
        {
            if (jobExecutionId.HasValue)
            {
                var job = await dbContext.JobExecutions.SingleAsync(
                    item => item.Id == jobExecutionId.Value, cancellationToken);
                job.ProgressPercent = CalculateProgressPercent(processed, candidates.Count);
                job.ProgressMessage = $"{processed}/{candidates.Count} CNPJs; {resolved} resolvidos; " +
                    $"{invalid} inválidos; {notFound} não encontrados; {pending} pendentes";
                job.ResultJson = JsonSerializer.Serialize(new
                {
                    total = candidates.Count,
                    processed,
                    resolved,
                    invalid,
                    notFound,
                    pending
                });
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public static decimal CalculateProgressPercent(int processed, int total) =>
        total <= 0 ? MaximumProcessingProgressPercent :
        Math.Clamp(Math.Round((decimal)processed / total * MaximumProcessingProgressPercent, 1),
            0, MaximumProcessingProgressPercent);
}
