using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class CustomerAddressCoordinateEnrichmentProcessorTests
{
    [Fact]
    public async Task ProcessAsync_GeocodesResolvedActiveAddressAndReportsResult()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db, isActive: true);
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerAddressCoordinateEnrichment,
            RelatedEntityId = fixture.ImportId, ParametersJson = "{\"customerStatus\":\"ACTIVE\",\"reprocessFailed\":false}",
            Queue = "default", Trigger = JobExecutionTrigger.Manual, Status = JobExecutionStatus.Processing, CreatedAt = DateTime.UtcNow };
        db.JobExecutions.Add(job); await db.SaveChangesAsync();
        var provider = new RecordingProvider();
        var processor = new CustomerAddressCoordinateEnrichmentProcessor(db, provider,
            Options.Create(new NominatimOptions { PersistenceBatchSize = 1 }));

        await processor.ProcessAsync(fixture.ImportId, job.Id, default);

        var coordinate = await db.CustomerAddressCoordinates.SingleAsync();
        Assert.Equal(CustomerAddressCoordinateStatuses.Resolved, coordinate.Status);
        Assert.Equal(-22.2m, coordinate.Latitude);
        Assert.Single(provider.Queries);
        Assert.Contains("\"resolved\":1", job.ResultJson);
        Assert.Contains("\"exactCoordinates\":1", job.ResultJson);
        Assert.Contains("\"interpolatedCoordinates\":0", job.ResultJson);
        Assert.Contains("\"streetCoordinates\":0", job.ResultJson);
        Assert.Contains("\"postalCodeCoordinates\":0", job.ResultJson);
        Assert.Contains("\"municipalityCoordinates\":0", job.ResultJson);
        Assert.Contains("\"skippedResolved\":0", job.ResultJson);
        Assert.Contains("\"providerRequests\":1", job.ResultJson);
    }

    [Fact]
    public async Task ProcessAsync_CountsInterpolatedCoordinateSeparatelyFromExact()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db, isActive: true);
        var job = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerAddressCoordinateEnrichment,
            RelatedEntityId = fixture.ImportId, ParametersJson = "{}", Queue = "default",
            Trigger = JobExecutionTrigger.Manual, Status = JobExecutionStatus.Processing, CreatedAt = DateTime.UtcNow
        };
        db.JobExecutions.Add(job);
        await db.SaveChangesAsync();
        var provider = new RecordingProvider(new AddressCoordinateLookup(
            CustomerAddressCoordinateStatuses.Resolved, -22.21m, -49.94m, "here-1", "Rua A, 15",
            "Coordenada interpolada.", CustomerAddressCoordinatePrecisions.Interpolated, "HERE_INTERPOLATED"));

        await new CustomerAddressCoordinateEnrichmentProcessor(db, provider, Options.Create(new NominatimOptions()))
            .ProcessAsync(fixture.ImportId, job.Id, default);

        var persisted = await db.CustomerAddressCoordinates.SingleAsync();
        Assert.Equal(CustomerAddressCoordinatePrecisions.Interpolated, persisted.Precision);
        Assert.Equal("HERE_INTERPOLATED", persisted.Source);
        Assert.Contains("\"resolved\":1", job.ResultJson);
        Assert.Contains("\"exactCoordinates\":0", job.ResultJson);
        Assert.Contains("\"interpolatedCoordinates\":1", job.ResultJson);
        Assert.Contains("1 interpolados", job.ProgressMessage);
    }

    [Fact]
    public async Task ProcessAsync_ReusesResolvedCoordinateForSameNormalizedAddress()
    {
        await using var db = CreateDb();
        var first = await SeedAsync(db, true, "001");
        var second = await SeedCustomerAsync(db, first, true, "002");
        var firstAddress = await db.CustomerRegistrationAddresses.SingleAsync(x => x.CustomerId == first.CustomerId);
        db.CustomerAddressCoordinates.Add(new CustomerAddressCoordinate { Id = Guid.NewGuid(), CustomerRegistrationAddressId = firstAddress.Id,
            NormalizedAddress = CustomerAddressCoordinateEnrichmentProcessor.NormalizeAddress(firstAddress), Source = "NOMINATIM",
            Status = CustomerAddressCoordinateStatuses.Resolved, Latitude = -22.2m, Longitude = -49.9m,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var provider = new RecordingProvider();
        await new CustomerAddressCoordinateEnrichmentProcessor(db, provider, Options.Create(new NominatimOptions()))
            .ProcessAsync(first.ImportId, default);
        Assert.Empty(provider.Queries);
        Assert.Equal(2, await db.CustomerAddressCoordinates.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_ReprocessFailedPersistsApproximateCoordinateOverExistingNotFoundRecord()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db, true);
        var address = await db.CustomerRegistrationAddresses.SingleAsync();
        var coordinate = new CustomerAddressCoordinate
        {
            Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id,
            NormalizedAddress = CustomerAddressCoordinateEnrichmentProcessor.NormalizeAddress(address), Source = "NOMINATIM",
            Status = CustomerAddressCoordinateStatuses.NotFound,
            FailureReason = "Endereço não encontrado.", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var job = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerAddressCoordinateEnrichment,
            RelatedEntityId = fixture.ImportId,
            ParametersJson = "{\"customerStatus\":\"ACTIVE\",\"reprocessFailed\":true}",
            Queue = "default", Trigger = JobExecutionTrigger.Manual,
            Status = JobExecutionStatus.Processing, CreatedAt = DateTime.UtcNow
        };
        db.AddRange(coordinate, job);
        await db.SaveChangesAsync();
        var provider = new RecordingProvider(new AddressCoordinateLookup(
            CustomerAddressCoordinateStatuses.Resolved, -22.21m, -49.94m, "10", "Rua A, Marília",
            "Coordenada aproximada pelo logradouro; número não confirmado.", AddressCoordinateMatchLevels.Street));

        await new CustomerAddressCoordinateEnrichmentProcessor(db, provider, Options.Create(new NominatimOptions()))
            .ProcessAsync(fixture.ImportId, job.Id, default);

        db.ChangeTracker.Clear();
        var persisted = await db.CustomerAddressCoordinates.SingleAsync();
        Assert.Equal(CustomerAddressCoordinateStatuses.Resolved, persisted.Status);
        Assert.Equal("TEST_STREET", persisted.Source);
        Assert.Equal(-22.21m, persisted.Latitude);
        Assert.Contains("aproximada", persisted.FailureReason);
        Assert.Contains("\"providerRequests\":1", job.ResultJson);
        Assert.Contains("\"skippedResolved\":0", job.ResultJson);
    }

    [Fact]
    public async Task ProcessAsync_SeparatesSkippedResolvedFromProviderRequests()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db, true);
        var address = await db.CustomerRegistrationAddresses.SingleAsync();
        db.CustomerAddressCoordinates.Add(new CustomerAddressCoordinate
        {
            Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id,
            NormalizedAddress = CustomerAddressCoordinateEnrichmentProcessor.NormalizeAddress(address),
            Source = "BRASIL_API_POSTAL_CODE", Status = CustomerAddressCoordinateStatuses.Resolved,
            Latitude = -22.2m, Longitude = -49.9m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        var job = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerAddressCoordinateEnrichment,
            RelatedEntityId = fixture.ImportId, ParametersJson = "{}", Queue = "default",
            Trigger = JobExecutionTrigger.Manual, Status = JobExecutionStatus.Processing, CreatedAt = DateTime.UtcNow
        };
        db.JobExecutions.Add(job);
        await db.SaveChangesAsync();
        var provider = new RecordingProvider();

        await new CustomerAddressCoordinateEnrichmentProcessor(db, provider, Options.Create(new NominatimOptions()))
            .ProcessAsync(fixture.ImportId, job.Id, default);

        Assert.Empty(provider.Queries);
        Assert.Contains("\"processed\":1", job.ResultJson);
        Assert.Contains("\"skippedResolved\":1", job.ResultJson);
        Assert.Contains("\"providerRequests\":0", job.ResultJson);
        Assert.Contains("\"resolved\":0", job.ResultJson);
    }

    [Fact]
    public async Task ProcessAsync_RefreshApproximatePromotesPostalCodeCoordinateUsingStreetAndNumber()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db, true);
        var address = await db.CustomerRegistrationAddresses.SingleAsync();
        db.CustomerAddressCoordinates.Add(new CustomerAddressCoordinate { Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id,
            NormalizedAddress = CustomerAddressCoordinateEnrichmentProcessor.NormalizeAddress(address), Source = "BRASIL_API_POSTAL_CODE",
            Status = CustomerAddressCoordinateStatuses.Resolved, Latitude = -22.3m, Longitude = -50.3m,
            FailureReason = "Coordenada aproximada pelo CEP.", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerAddressCoordinateEnrichment,
            RelatedEntityId = fixture.ImportId, ParametersJson = "{\"customerStatus\":\"ACTIVE\",\"refreshApproximate\":true}",
            Queue = "default", Trigger = JobExecutionTrigger.Manual, Status = JobExecutionStatus.Processing, CreatedAt = DateTime.UtcNow };
        db.JobExecutions.Add(job); await db.SaveChangesAsync();
        var provider = new RecordingProvider(new AddressCoordinateLookup(CustomerAddressCoordinateStatuses.Resolved,
            -22.21m, -49.94m, "exact", "Rua A, 10", null, AddressCoordinateMatchLevels.Exact, "GEOAPIFY_EXACT"));

        await new CustomerAddressCoordinateEnrichmentProcessor(db, provider, Options.Create(new NominatimOptions()))
            .ProcessAsync(fixture.ImportId, job.Id, default);

        var persisted = await db.CustomerAddressCoordinates.SingleAsync();
        Assert.Equal("GEOAPIFY_EXACT", persisted.Source);
        Assert.Equal(-22.21m, persisted.Latitude);
        Assert.Single(provider.Queries);
        Assert.Contains("\"refreshedApproximate\":1", job.ResultJson);
    }

    [Fact]
    public async Task ProcessAsync_RefreshApproximateRetainsPostalCodeCoordinateWhenProviderDoesNotResolveStreet()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db, true);
        var address = await db.CustomerRegistrationAddresses.SingleAsync();
        db.CustomerAddressCoordinates.Add(new CustomerAddressCoordinate { Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id,
            NormalizedAddress = CustomerAddressCoordinateEnrichmentProcessor.NormalizeAddress(address), Source = "BRASIL_API_POSTAL_CODE",
            Status = CustomerAddressCoordinateStatuses.Resolved, Latitude = -22.3m, Longitude = -50.3m,
            FailureReason = "Coordenada aproximada pelo CEP.", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerAddressCoordinateEnrichment,
            RelatedEntityId = fixture.ImportId, ParametersJson = "{\"refreshApproximate\":true}", Queue = "default",
            Trigger = JobExecutionTrigger.Manual, Status = JobExecutionStatus.Processing, CreatedAt = DateTime.UtcNow };
        db.JobExecutions.Add(job); await db.SaveChangesAsync();
        var provider = new RecordingProvider(new AddressCoordinateLookup(CustomerAddressCoordinateStatuses.NotFound,
            null, null, null, null, "Não encontrado."));

        await new CustomerAddressCoordinateEnrichmentProcessor(db, provider, Options.Create(new NominatimOptions()))
            .ProcessAsync(fixture.ImportId, job.Id, default);

        var persisted = await db.CustomerAddressCoordinates.SingleAsync();
        Assert.Equal(CustomerAddressCoordinateStatuses.Resolved, persisted.Status);
        Assert.Equal("BRASIL_API_POSTAL_CODE", persisted.Source);
        Assert.Equal(-22.3m, persisted.Latitude);
        Assert.Contains("\"retainedApproximate\":1", job.ResultJson);
    }

    [Theory]
    [InlineData("NOMINATIM", AddressCoordinateMatchLevels.Exact)]
    [InlineData("HERE_INTERPOLATED", CustomerAddressCoordinatePrecisions.Interpolated)]
    [InlineData("NOMINATIM_STREET", AddressCoordinateMatchLevels.Street)]
    [InlineData("GEOAPIFY_STREET", AddressCoordinateMatchLevels.Street)]
    [InlineData("BRASIL_API_POSTAL_CODE", AddressCoordinateMatchLevels.PostalCode)]
    [InlineData("NOMINATIM_MUNICIPALITY", AddressCoordinateMatchLevels.Municipality)]
    [InlineData("GEOAPIFY_MUNICIPALITY", AddressCoordinateMatchLevels.Municipality)]
    public void MatchLevelFromSource_ClassifiesPersistedPrecision(string source, string expected) =>
        Assert.Equal(expected, CustomerAddressCoordinateEnrichmentProcessor.MatchLevelFromSource(source));
    [Theory]
    [InlineData("ACTIVE", false)]
    [InlineData("INACTIVE", true)]
    [InlineData("ALL", true)]
    public async Task ProcessAsync_AppliesCustomerStatusBeforeCallingProvider(string filter, bool expectedCall)
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db, false);
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerAddressCoordinateEnrichment,
            RelatedEntityId = fixture.ImportId, ParametersJson = $"{{\"customerStatus\":\"{filter}\"}}", Queue = "default",
            Trigger = JobExecutionTrigger.Manual, Status = JobExecutionStatus.Processing, CreatedAt = DateTime.UtcNow };
        db.JobExecutions.Add(job); await db.SaveChangesAsync();
        var provider = new RecordingProvider();
        await new CustomerAddressCoordinateEnrichmentProcessor(db, provider, Options.Create(new NominatimOptions()))
            .ProcessAsync(fixture.ImportId, job.Id, default);
        Assert.Equal(expectedCall, provider.Queries.Count == 1);
    }

    [Fact]
    public async Task ProcessAsync_StopsAtConfiguredExternalRequestLimit()
    {
        await using var db = CreateDb();
        var first = await SeedAsync(db, true, "001");
        await SeedCustomerAsync(db, first, true, "002");
        var job = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerAddressCoordinateEnrichment,
            RelatedEntityId = first.ImportId,
            ParametersJson = "{\"customerStatus\":\"ACTIVE\",\"reprocessFailed\":true,\"maximumRequests\":1}",
            Queue = "default", Trigger = JobExecutionTrigger.Manual,
            Status = JobExecutionStatus.Processing, CreatedAt = DateTime.UtcNow
        };
        db.JobExecutions.Add(job); await db.SaveChangesAsync();
        var provider = new RecordingProvider();

        await new CustomerAddressCoordinateEnrichmentProcessor(db, provider, Options.Create(new NominatimOptions()))
            .ProcessAsync(first.ImportId, job.Id, default);

        Assert.Single(provider.Queries);
        Assert.Contains("\"providerRequests\":1", job.ResultJson);
    }

    [Theory]
    [InlineData("{\"maximumRequests\":1}", 1)]
    [InlineData("{}", null)]
    public void ReadMaximumRequests_ParsesOptionalLimit(string json, int? expected)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(expected, CustomerAddressCoordinateEnrichmentProcessor.ReadMaximumRequests(document.RootElement));
    }

    private static ImportDbContext CreateDb() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"address-coordinates-{Guid.NewGuid()}").Options);

    private static async Task<(Guid ImportId, Guid SourceId, Guid MunicipalityId, Guid CustomerId)> SeedAsync(ImportDbContext db, bool isActive, string code = "001")
    {
        var now = DateTime.UtcNow; var sourceId = Guid.NewGuid(); var importId = Guid.NewGuid(); var municipalityId = Guid.NewGuid();
        db.DataSources.Add(new DataSource { Id = sourceId, Code = "CUSTOMERS", ProcessorKey = "customers", Name = "Clientes", Type = "XLSX",
            ImportMode = DataSourceImportMode.Snapshot, NextImportVersion = 2, Active = true, CreatedAt = now, UpdatedAt = now });
        db.RouteImports.Add(new RouteImport { Id = importId, DataSourceId = sourceId, Version = 1, FileName = "x", FilePath = "x",
            Status = RouteImportStatus.Completed, CreatedAt = now });
        db.Municipalities.Add(new Municipality { Id = municipalityId, Name = "Marília", NormalizedName = "MARILIA", StateCode = "SP", CreatedAt = now });
        await db.SaveChangesAsync();
        var result = (importId, sourceId, municipalityId, Guid.Empty);
        var customerId = await SeedCustomerAsync(db, result, isActive, code);
        return (importId, sourceId, municipalityId, customerId);
    }

    private static async Task<Guid> SeedCustomerAsync(ImportDbContext db, (Guid ImportId, Guid SourceId, Guid MunicipalityId, Guid CustomerId) fixture, bool active, string code)
    {
        var now = DateTime.UtcNow; var customerId = Guid.NewGuid();
        db.Customers.Add(new Customer { Id = customerId, DataSourceId = fixture.SourceId, BranchCode = "01", ExternalCode = code, IsActive = active, CreatedAt = now });
        db.CustomerSnapshots.Add(new CustomerSnapshot { Id = Guid.NewGuid(), ImportId = fixture.ImportId, CustomerId = customerId,
            MunicipalityId = fixture.MunicipalityId, DocumentNumber = code, DocumentType = "CNPJ", LegalName = code, TradeName = code,
            CustomerType = "Cliente", SourceRowNumber = 1, CreatedAt = now });
        db.CustomerRegistrationAddresses.Add(new CustomerRegistrationAddress { Id = Guid.NewGuid(), CustomerId = customerId,
            DocumentNumber = code, Source = "BRASIL_API", Status = CustomerRegistrationAddressStatuses.Resolved,
            Street = "Rua A", Number = "10", Neighborhood = "Centro", City = "Marília", StateCode = "SP", PostalCode = "17500-000",
            CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync(); return customerId;
    }

    private sealed class RecordingProvider(AddressCoordinateLookup? lookup = null) : ICustomerAddressCoordinateProvider
    {
        public string SourceName => "TEST";
        public List<AddressCoordinateQuery> Queries { get; } = [];
        public Task<AddressCoordinateLookup> FindAsync(AddressCoordinateQuery query, CancellationToken cancellationToken)
        { Queries.Add(query); return Task.FromResult(lookup ?? new AddressCoordinateLookup("RESOLVED", -22.2m, -49.9m, "1", "Rua A", null)); }
    }
}
