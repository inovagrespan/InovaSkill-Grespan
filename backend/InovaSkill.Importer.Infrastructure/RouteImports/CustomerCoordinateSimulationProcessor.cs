using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class CustomerCoordinateSimulationProcessor(
    ImportDbContext db,
    INearbyBuildingCoordinateProvider provider,
    ICustomerAddressCoordinateProvider? addressProvider = null) : IProgressReportingOperationalJobProcessor
{
    public const string ApplyAction = "APPLY";
    public const string RevertAction = "REVERT";
    // Persistir cada atribuição isoladamente evita que uma colisão inesperada no
    // índice parcial de coordenadas descarte um lote inteiro já preparado.
    private const int PersistenceBatchSize = 1;
    private const int IndividualAddressFallbackTimeoutSeconds = 10;
    private const int CityPoolMaximumWaitSeconds = 25;
    private const decimal MaximumProcessingProgressPercent = 99m;
    private const string SimulatedRegistrationAddressSource = "COORDINATE_SIMULATION";
    public string JobType => OperationalJobCodes.CustomerCoordinateSimulation;

    public Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken) =>
        ProcessAsync(relatedEntityId, Guid.Empty, cancellationToken);

    public async Task ProcessAsync(Guid customerImportId, Guid jobExecutionId, CancellationToken cancellationToken)
    {
        var job = await db.JobExecutions.SingleAsync(item => item.Id == jobExecutionId, cancellationToken);
        using var document = JsonDocument.Parse(job.ParametersJson);
        var action = ReadAction(document.RootElement);
        if (action == RevertAction)
        {
            await RevertAsync(job, ReadSourceJobExecutionId(document.RootElement), cancellationToken);
            return;
        }
        await ApplyAsync(customerImportId, job, cancellationToken);
    }

    public static string ReadAction(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("action", out var value) || value.ValueKind != JsonValueKind.String)
            return ApplyAction;
        var action = value.GetString()?.Trim().ToUpperInvariant();
        return action is ApplyAction or RevertAction
            ? action
            : throw new ArgumentException("$.action deve ser APPLY ou REVERT.");
    }

    public static Guid ReadSourceJobExecutionId(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("sourceJobExecutionId", out var value) ||
            value.ValueKind != JsonValueKind.String || !Guid.TryParse(value.GetString(), out var id))
            throw new ArgumentException("$.sourceJobExecutionId é obrigatório para reversão.");
        return id;
    }

    public static bool ReadRecalculateDependents(JsonElement parameters) =>
        !parameters.TryGetProperty("recalculateDependents", out var value) || value.ValueKind == JsonValueKind.Null
            ? true
            : value.ValueKind == JsonValueKind.True ? true
            : value.ValueKind == JsonValueKind.False ? false
            : throw new ArgumentException("$.recalculateDependents deve ser booleano.");

    public static bool ReadRecalculateDependents(string parametersJson)
    {
        using var document = JsonDocument.Parse(parametersJson);
        return ReadRecalculateDependents(document.RootElement);
    }

    private async Task ApplyAsync(Guid customerImportId, JobExecution job, CancellationToken cancellationToken)
    {
        var addressesWithActiveSimulation = await db.CustomerCoordinateSimulationAudits.AsNoTracking()
            .Where(audit => audit.RevertedAt == null)
            .Select(audit => audit.CustomerRegistrationAddressId)
            .ToListAsync(cancellationToken);
        var existingCoordinates = await db.CustomerAddressCoordinates.AsNoTracking()
            .Where(coordinate => coordinate.Status == CustomerAddressCoordinateStatuses.Resolved &&
                coordinate.Precision == CustomerAddressCoordinatePrecisions.Exact &&
                !CustomerAddressCoordinateQuality.ApproximateSources.Contains(coordinate.Source) &&
                coordinate.Latitude != null && coordinate.Longitude != null)
            .Select(coordinate => new { coordinate.Latitude, coordinate.Longitude })
            .ToListAsync(cancellationToken);
        var usedCoordinateKeys = existingCoordinates
            .Select(coordinate => CoordinateKey(coordinate.Latitude!.Value, coordinate.Longitude!.Value))
            .ToHashSet(StringComparer.Ordinal);
        var activeAuditCoordinates = await db.CustomerCoordinateSimulationAudits.AsNoTracking()
            .Where(audit => audit.RevertedAt == null)
            .Select(audit => new { audit.SimulatedLatitude, audit.SimulatedLongitude })
            .ToListAsync(cancellationToken);
        foreach (var coordinate in activeAuditCoordinates)
            usedCoordinateKeys.Add(CoordinateKey(coordinate.SimulatedLatitude,
                coordinate.SimulatedLongitude));
        var candidates = await db.CustomerSnapshots
            .Where(snapshot => snapshot.ImportId == customerImportId)
            .Include(snapshot => snapshot.Customer).ThenInclude(customer => customer!.RegistrationAddress)
                .ThenInclude(address => address!.Coordinate)
            .Include(snapshot => snapshot.Municipality).ThenInclude(municipality => municipality!.Coordinate)
            .OrderBy(snapshot => snapshot.Customer!.ExternalCode)
            .Where(snapshot => snapshot.Customer!.RegistrationAddress == null ||
                (!addressesWithActiveSimulation.Contains(snapshot.Customer.RegistrationAddress.Id) &&
                (snapshot.Customer.RegistrationAddress.Coordinate == null ||
                 snapshot.Customer.RegistrationAddress.Coordinate.Status != CustomerAddressCoordinateStatuses.Resolved ||
                 snapshot.Customer.RegistrationAddress.Coordinate.Precision != CustomerAddressCoordinatePrecisions.Exact ||
                 CustomerAddressCoordinateQuality.ApproximateSources.Contains(snapshot.Customer.RegistrationAddress.Coordinate.Source) ||
                 snapshot.Customer.RegistrationAddress.Coordinate.Latitude == null ||
                 snapshot.Customer.RegistrationAddress.Coordinate.Longitude == null)))
            .ToListAsync(cancellationToken);

        var applied = 0;
        var withoutBase = 0;
        var notFound = 0;
        var exhaustedBaseKeys = new HashSet<string>(StringComparer.Ordinal);
        var cityAddressPools = await DiscoverCityPoolsAsync(candidates, cancellationToken);
        for (var index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await CancellationWasRequested(job.Id, cancellationToken)) break;
            var snapshot = candidates[index];
            var address = snapshot.Customer!.RegistrationAddress;
            var current = address?.Coordinate;
            var baseLatitude = current?.Latitude ?? snapshot.Municipality?.Coordinate?.Latitude;
            var baseLongitude = current?.Longitude ?? snapshot.Municipality?.Coordinate?.Longitude;
            var baseSource = current?.Latitude is not null && current.Longitude is not null
                ? current.Source
                : "MUNICIPALITY";
            var city = snapshot.Municipality?.Name;
            var stateCode = snapshot.Municipality?.StateCode;
            if (baseLatitude is null || baseLongitude is null || string.IsNullOrWhiteSpace(city) ||
                string.IsNullOrWhiteSpace(stateCode))
            {
                withoutBase++;
                await SaveProgress(index + 1);
                continue;
            }

            var baseKey = $"{CoordinateKey(baseLatitude.Value, baseLongitude.Value)}|{city}|{stateCode}";
            if (exhaustedBaseKeys.Contains(baseKey) && address is null)
            {
                notFound++;
                await SaveProgress(index + 1);
                continue;
            }

            NearbyBuildingCoordinate? resolved = null;
            cityAddressPools.TryGetValue($"{city}|{stateCode}", out var pool);
            pool ??= Array.Empty<NearbyBuildingCoordinate>();
            resolved = pool.FirstOrDefault(candidate =>
                !usedCoordinateKeys.Contains(GeoapifyNearbyBuildingCoordinateProvider.CoordinateKey(
                    candidate.Latitude, candidate.Longitude)));
            if (resolved is null && pool.Count == 0 && address is null)
                resolved = await provider.FindAsync(baseLatitude.Value, baseLongitude.Value,
                    city, stateCode, usedCoordinateKeys, cancellationToken);
            if (resolved is null)
                resolved = await FindRegisteredAddressAsync(address, city, stateCode,
                    baseLatitude.Value, baseLongitude.Value, usedCoordinateKeys, cancellationToken);
            if (resolved is null && address is not null)
                resolved = await provider.FindAsync(baseLatitude.Value, baseLongitude.Value,
                    city, stateCode, usedCoordinateKeys, cancellationToken);
            var municipalityLatitude = snapshot.Municipality?.Coordinate?.Latitude;
            var municipalityLongitude = snapshot.Municipality?.Coordinate?.Longitude;
            if (resolved is null && current?.Latitude is not null && current.Longitude is not null &&
                municipalityLatitude is not null && municipalityLongitude is not null &&
                (municipalityLatitude != baseLatitude || municipalityLongitude != baseLongitude))
            {
                baseLatitude = municipalityLatitude;
                baseLongitude = municipalityLongitude;
                baseSource = "MUNICIPALITY_FALLBACK";
                resolved = await provider.FindAsync(baseLatitude.Value, baseLongitude.Value,
                    city, stateCode, usedCoordinateKeys, cancellationToken);
            }
            if (resolved is null || !usedCoordinateKeys.Add(CoordinateKey(resolved.Latitude, resolved.Longitude)))
            {
                if (address is null) exhaustedBaseKeys.Add(baseKey);
                notFound++;
                await SaveProgress(index + 1);
                continue;
            }
            if (await db.CustomerCoordinateSimulationAudits.AsNoTracking().AnyAsync(audit =>
                audit.RevertedAt == null && audit.SimulatedLatitude == resolved.Latitude &&
                audit.SimulatedLongitude == resolved.Longitude, cancellationToken))
            {
                notFound++;
                await SaveProgress(index + 1);
                continue;
            }

            var now = DateTime.UtcNow;
            var addressCreatedBySimulation = address is null;
            if (address is null)
            {
                address = new CustomerRegistrationAddress
                {
                    Id = Guid.NewGuid(),
                    CustomerId = snapshot.Customer.Id,
                    DocumentNumber = snapshot.DocumentNumber,
                    Source = SimulatedRegistrationAddressSource,
                    Status = CustomerRegistrationAddressStatuses.Resolved,
                    City = city,
                    StateCode = stateCode,
                    FailureReason = "Endereço municipal criado somente para vincular a coordenada simulada.",
                    LastAttemptAt = now,
                    ResolvedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.CustomerRegistrationAddresses.Add(address);
                snapshot.Customer.RegistrationAddress = address;
            }
            db.CustomerCoordinateSimulationAudits.Add(CreateAudit(job, address, current,
                baseLatitude.Value, baseLongitude.Value, baseSource, resolved,
                addressCreatedBySimulation, now));
            current ??= new CustomerAddressCoordinate
            {
                Id = Guid.NewGuid(),
                CustomerRegistrationAddressId = address.Id,
                CreatedAt = now
            };
            current.NormalizedAddress = CustomerAddressCoordinateEnrichmentProcessor.NormalizeAddress(address);
            current.Source = resolved.Source;
            current.Status = CustomerAddressCoordinateStatuses.Resolved;
            current.Precision = CustomerAddressCoordinatePrecisions.Exact;
            current.Latitude = resolved.Latitude;
            current.Longitude = resolved.Longitude;
            current.ProviderPlaceId = resolved.PlaceId;
            current.DisplayName = resolved.DisplayName;
            current.FailureReason = null;
            current.LastAttemptAt = now;
            current.ResolvedAt = now;
            current.UpdatedAt = now;
            if (address.Coordinate is null)
            {
                db.CustomerAddressCoordinates.Add(current);
                address.Coordinate = current;
            }
            applied++;
            await SaveProgress(index + 1);
        }
        await SaveProgress(candidates.Count, force: true);
        job.ResultJson = JsonSerializer.Serialize(new
        {
            action = ApplyAction,
            total = candidates.Count,
            applied,
            withoutBase,
            notFound,
            pending = withoutBase + notFound
        });
        job.ProgressMessage = $"{applied} coordenada(s) simulada(s); {withoutBase + notFound} pendente(s)";
        await db.SaveChangesAsync(cancellationToken);

        async Task<Dictionary<string, IReadOnlyList<NearbyBuildingCoordinate>>>
            DiscoverCityPoolsAsync(IReadOnlyList<CustomerSnapshot> snapshots, CancellationToken token)
        {
            var requests = new Dictionary<string, Task<IReadOnlyList<NearbyBuildingCoordinate>>>(StringComparer.Ordinal);
            foreach (var item in snapshots)
            {
                var registration = item.Customer?.RegistrationAddress;
                var coordinate = registration?.Coordinate;
                var latitude = coordinate?.Latitude ?? item.Municipality?.Coordinate?.Latitude;
                var longitude = coordinate?.Longitude ?? item.Municipality?.Coordinate?.Longitude;
                var cityName = item.Municipality?.Name;
                var state = item.Municipality?.StateCode;
                if (latitude is null || longitude is null || string.IsNullOrWhiteSpace(cityName) ||
                    string.IsNullOrWhiteSpace(state))
                    continue;
                var key = $"{cityName}|{state}";
                if (!requests.ContainsKey(key))
                    requests[key] = DiscoverWithDeadlineAsync(latitude.Value, longitude.Value,
                        cityName, state, token);
            }
            await Task.WhenAll(requests.Values);
            return requests.ToDictionary(pair => pair.Key, pair => pair.Value.Result,
                StringComparer.Ordinal);

            async Task<IReadOnlyList<NearbyBuildingCoordinate>> DiscoverWithDeadlineAsync(
                decimal latitude, decimal longitude, string city, string state, CancellationToken cancellation)
            {
                try
                {
                    return await provider.DiscoverCityAddressesAsync(latitude, longitude, city, state, cancellation)
                        .WaitAsync(TimeSpan.FromSeconds(CityPoolMaximumWaitSeconds), cancellation);
                }
                catch (TimeoutException)
                {
                    return Array.Empty<NearbyBuildingCoordinate>();
                }
            }
        }

        async Task SaveProgress(int processed, bool force = false)
        {
            if (!force && processed % PersistenceBatchSize != 0) return;
            job.ProgressPercent = candidates.Count == 0 ? MaximumProcessingProgressPercent :
                Math.Clamp(decimal.Round((decimal)processed / candidates.Count *
                    MaximumProcessingProgressPercent, 1), 0, MaximumProcessingProgressPercent);
            job.ProgressMessage = $"{processed}/{candidates.Count}; {applied} simuladas; {withoutBase + notFound} pendentes";
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres &&
                postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgres.ConstraintName == "IX_customer_coordinate_simulation_audits_active_coordinate")
            {
                var discardedAudits = db.ChangeTracker.Entries<CustomerCoordinateSimulationAudit>()
                    .Count(entry => entry.State == EntityState.Added);
                foreach (var entry in db.ChangeTracker.Entries()
                    .Where(entry => entry.Entity != job &&
                        entry.State is EntityState.Added or EntityState.Modified))
                    entry.State = EntityState.Detached;
                // A corrida é descartada sem perder o progresso já persistido;
                // a próxima execução seleciona outro endereço não utilizado.
                applied = Math.Max(0, applied - discardedAudits);
                notFound += discardedAudits;
                db.Entry(job).State = EntityState.Modified;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task<NearbyBuildingCoordinate?> FindRegisteredAddressAsync(
        CustomerRegistrationAddress? address,
        string city,
        string stateCode,
        decimal baseLatitude,
        decimal baseLongitude,
        IReadOnlySet<string> usedCoordinateKeys,
        CancellationToken cancellationToken)
    {
        if (addressProvider is null || address is null || string.IsNullOrWhiteSpace(address.Street))
            return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(IndividualAddressFallbackTimeoutSeconds));
        AddressCoordinateLookup lookup;
        try
        {
            lookup = await addressProvider.FindAsync(new AddressCoordinateQuery(
                address.StreetType, address.Street, address.Number, address.Neighborhood,
                city, stateCode, address.PostalCode), timeout.Token);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        if (lookup.Status != "RESOLVED" || lookup.Latitude is null || lookup.Longitude is null)
            return null;
        var distance = GeoapifyNearbyBuildingCoordinateProvider.DistanceMeters(
            baseLatitude, baseLongitude, lookup.Latitude.Value, lookup.Longitude.Value);
        var key = GeoapifyNearbyBuildingCoordinateProvider.CoordinateKey(
            lookup.Latitude.Value, lookup.Longitude.Value);
        return distance <= GeoapifyNearbyBuildingCoordinateProvider.MaximumDistanceMeters &&
            !usedCoordinateKeys.Contains(key)
            ? new NearbyBuildingCoordinate(lookup.Latitude.Value, lookup.Longitude.Value,
                lookup.PlaceId ?? $"address:{key}", lookup.DisplayName ?? address.Street,
                distance, $"{lookup.Source ?? "GEOCODER"}_SIMULATED_ADDRESS")
            : null;
    }

    private static string CoordinateKey(decimal latitude, decimal longitude) =>
        $"{decimal.Round(latitude, 6):0.000000}|{decimal.Round(longitude, 6):0.000000}";

    private async Task RevertAsync(JobExecution job, Guid sourceJobExecutionId, CancellationToken cancellationToken)
    {
        var audits = await db.CustomerCoordinateSimulationAudits
            .Include(audit => audit.CustomerRegistrationAddress).ThenInclude(address => address!.Coordinate)
            .Where(audit => audit.JobExecutionId == sourceJobExecutionId && audit.RevertedAt == null)
            .OrderBy(audit => audit.AppliedAt).ToListAsync(cancellationToken);
        if (audits.Count == 0)
            throw new InvalidOperationException("A execução não possui coordenadas simuladas ativas para reverter.");
        var reverted = 0;
        var conflicts = 0;
        foreach (var audit in audits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var coordinate = audit.CustomerRegistrationAddress?.Coordinate;
            var address = audit.CustomerRegistrationAddress;
            if (coordinate is null || address is null || coordinate.Source != audit.SimulatedSource ||
                coordinate.Latitude != audit.SimulatedLatitude || coordinate.Longitude != audit.SimulatedLongitude ||
                coordinate.ProviderPlaceId != audit.ProviderPlaceId ||
                (audit.RegistrationAddressCreatedBySimulation &&
                 address.Source != SimulatedRegistrationAddressSource))
            {
                conflicts++;
                continue;
            }
            if (audit.RegistrationAddressCreatedBySimulation)
            {
                db.CustomerRegistrationAddresses.Remove(address);
            }
            else if (!audit.OriginalCoordinateExisted)
            {
                db.CustomerAddressCoordinates.Remove(coordinate);
            }
            else
            {
                coordinate.NormalizedAddress = audit.OriginalNormalizedAddress!;
                coordinate.Source = audit.OriginalSource!;
                coordinate.Status = audit.OriginalStatus!;
                coordinate.Precision = audit.OriginalPrecision!;
                coordinate.Latitude = audit.OriginalLatitude;
                coordinate.Longitude = audit.OriginalLongitude;
                coordinate.ProviderPlaceId = audit.OriginalProviderPlaceId;
                coordinate.DisplayName = audit.OriginalDisplayName;
                coordinate.FailureReason = audit.OriginalFailureReason;
                coordinate.LastAttemptAt = audit.OriginalLastAttemptAt;
                coordinate.ResolvedAt = audit.OriginalResolvedAt;
                coordinate.CreatedAt = audit.OriginalCreatedAt!.Value;
                coordinate.UpdatedAt = audit.OriginalUpdatedAt!.Value;
            }
            audit.RevertJobExecutionId = job.Id;
            audit.RevertedByUserId = job.RequestedByUserId;
            audit.RevertedAt = DateTime.UtcNow;
            reverted++;
        }
        job.ResultJson = JsonSerializer.Serialize(new
        {
            action = RevertAction,
            sourceJobExecutionId,
            total = audits.Count,
            reverted,
            conflicts
        });
        job.ProgressMessage = $"{reverted} coordenada(s) restaurada(s); {conflicts} conflito(s)";
        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> CancellationWasRequested(Guid jobId, CancellationToken cancellationToken) =>
        db.JobExecutions.AsNoTracking().AnyAsync(item => item.Id == jobId &&
            item.CancellationRequestedAt != null, cancellationToken);

    private static CustomerCoordinateSimulationAudit CreateAudit(
        JobExecution job,
        CustomerRegistrationAddress address,
        CustomerAddressCoordinate? original,
        decimal baseLatitude,
        decimal baseLongitude,
        string baseSource,
        NearbyBuildingCoordinate resolved,
        bool addressCreatedBySimulation,
        DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        JobExecutionId = job.Id,
        CustomerId = address.CustomerId,
        CustomerRegistrationAddressId = address.Id,
        RegistrationAddressCreatedBySimulation = addressCreatedBySimulation,
        OriginalCoordinateExisted = original is not null,
        OriginalNormalizedAddress = original?.NormalizedAddress,
        OriginalSource = original?.Source,
        OriginalStatus = original?.Status,
        OriginalPrecision = original?.Precision,
        OriginalLatitude = original?.Latitude,
        OriginalLongitude = original?.Longitude,
        OriginalProviderPlaceId = original?.ProviderPlaceId,
        OriginalDisplayName = original?.DisplayName,
        OriginalFailureReason = original?.FailureReason,
        OriginalLastAttemptAt = original?.LastAttemptAt,
        OriginalResolvedAt = original?.ResolvedAt,
        OriginalCreatedAt = original?.CreatedAt,
        OriginalUpdatedAt = original?.UpdatedAt,
        BaseLatitude = baseLatitude,
        BaseLongitude = baseLongitude,
        BaseSource = baseSource,
        SimulatedLatitude = resolved.Latitude,
        SimulatedLongitude = resolved.Longitude,
        SimulatedSource = resolved.Source,
        ProviderPlaceId = resolved.PlaceId,
        DisplayName = resolved.DisplayName,
        DistanceMeters = resolved.DistanceMeters,
        AppliedByUserId = job.RequestedByUserId ?? throw new InvalidOperationException(
            "O job de simulação precisa identificar o administrador solicitante."),
        AppliedAt = now
    };
}
