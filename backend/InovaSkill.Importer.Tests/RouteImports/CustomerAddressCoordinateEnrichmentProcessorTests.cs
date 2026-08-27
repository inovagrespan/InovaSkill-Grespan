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
    public async Task ProcessAsync_ReprocessFailed_UpdatesExistingCoordinateAndProviderSource()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db, true);
        var address = await db.CustomerRegistrationAddresses.SingleAsync();
        db.CustomerAddressCoordinates.Add(new CustomerAddressCoordinate
        {
            Id = Guid.NewGuid(),
            CustomerRegistrationAddressId = address.Id,
            NormalizedAddress = CustomerAddressCoordinateEnrichmentProcessor.NormalizeAddress(address),
            Source = "NOMINATIM",
            Status = CustomerAddressCoordinateStatuses.NotFound,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        var job = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobType = OperationalJobCodes.CustomerAddressCoordinateEnrichment,
            RelatedEntityId = fixture.ImportId,
            ParametersJson = "{\"customerStatus\":\"ACTIVE\",\"reprocessFailed\":true}",
            Queue = "default",
            Trigger = JobExecutionTrigger.Manual,
            Status = JobExecutionStatus.Processing,
            CreatedAt = DateTime.UtcNow
        };
        db.JobExecutions.Add(job);
        await db.SaveChangesAsync();

        await new CustomerAddressCoordinateEnrichmentProcessor(
            db, new RecordingProvider(), Options.Create(new NominatimOptions { PersistenceBatchSize = 1 }))
            .ProcessAsync(fixture.ImportId, job.Id, default);

        var coordinate = await db.CustomerAddressCoordinates.SingleAsync();
        Assert.Equal(CustomerAddressCoordinateStatuses.Resolved, coordinate.Status);
        Assert.Equal("TEST", coordinate.Source);
        Assert.Equal(-22.2m, coordinate.Latitude);
    }

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
        Assert.Contains("\"externalRequests\":1", job.ResultJson);
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

    private sealed class RecordingProvider : ICustomerAddressCoordinateProvider
    {
        public string SourceName => "TEST";
        public List<AddressCoordinateQuery> Queries { get; } = [];
        public Task<AddressCoordinateLookup> FindAsync(AddressCoordinateQuery query, CancellationToken cancellationToken)
        { Queries.Add(query); return Task.FromResult(new AddressCoordinateLookup("RESOLVED", -22.2m, -49.9m, "1", "Rua A", null)); }
    }
}
