using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class CustomerRegistrationAddressEnrichmentProcessorTests
{
    [Fact]
    public async Task ProcessAsync_EnrichesOnlyCnpjWithoutFinalResultAndRetriesFailedRecord()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);
        var failedAddress = new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = fixture.FailedCustomerId,
            DocumentNumber = "22222222000122", Source = "BRASIL_API",
            Status = CustomerRegistrationAddressStatuses.Failed,
            FailureReason = "Indisponível", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var resolvedAddress = new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = fixture.ResolvedCustomerId,
            DocumentNumber = "33333333000133", Source = "BRASIL_API",
            Status = CustomerRegistrationAddressStatuses.Resolved,
            City = "BAURU", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.AddRange(failedAddress, resolvedAddress);
        await db.SaveChangesAsync();
        var provider = new RecordingProvider();

        await CreateProcessor(db, provider)
            .ProcessAsync(fixture.ImportId, CancellationToken.None);

        Assert.Equal(
            new[] { "11111111000111", "22222222000122" },
            provider.Cnpjs.OrderBy(cnpj => cnpj));
        var addresses = await db.CustomerRegistrationAddresses.OrderBy(item => item.DocumentNumber).ToListAsync();
        Assert.Equal(3, addresses.Count);
        Assert.Equal(2, addresses.Count(item => item.City == "MARILIA"));
        Assert.Equal("BAURU", addresses.Single(item => item.CustomerId == fixture.ResolvedCustomerId).City);
        Assert.DoesNotContain(addresses, item => item.CustomerId == fixture.CpfCustomerId);
    }

    [Fact]
    public async Task ProcessAsync_IsIdempotentAfterSuccessfulEnrichment()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);
        var provider = new RecordingProvider();
        var processor = CreateProcessor(db, provider);

        await processor.ProcessAsync(fixture.ImportId, CancellationToken.None);
        await processor.ProcessAsync(fixture.ImportId, CancellationToken.None);

        Assert.Equal(3, provider.Cnpjs.Count);
        Assert.Equal(3, await db.CustomerRegistrationAddresses.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_PersistsTechnicalFailureAndRethrowsForJobRetry()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);
        var processor = CreateProcessor(db, new FailingProvider());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            processor.ProcessAsync(fixture.ImportId, CancellationToken.None));

        var failure = await db.CustomerRegistrationAddresses.SingleAsync();
        Assert.Equal(CustomerRegistrationAddressStatuses.Failed, failure.Status);
        Assert.Equal("BrasilAPI indisponível.", failure.FailureReason);
        Assert.Null(failure.ResolvedAt);
    }

    [Fact]
    public async Task ProcessAsync_LeavesRateLimitedCustomerPendingAndContinues()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);

        await CreateProcessor(db, new RateLimitedThenSuccessfulProvider())
            .ProcessAsync(fixture.ImportId, CancellationToken.None);

        Assert.Equal(2, await db.CustomerRegistrationAddresses.CountAsync());
        Assert.DoesNotContain(await db.CustomerRegistrationAddresses.ToListAsync(),
            address => address.DocumentNumber == "11111111000111");
    }

    [Fact]
    public async Task ProcessAsync_ReportsBoundedProgressAndOutcomeCountsInJobExecution()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);
        var job = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerRegistrationAddressEnrichment,
            ContractVersion = 1, Queue = BackgroundJobQueues.Default, Trigger = JobExecutionTrigger.Manual,
            ParametersJson = "{}", Status = JobExecutionStatus.Processing,
            RelatedEntityId = fixture.ImportId, CreatedAt = DateTime.UtcNow
        };
        db.JobExecutions.Add(job);
        await db.SaveChangesAsync();
        var processor = new CustomerRegistrationAddressEnrichmentProcessor(
            db, new RecordingProvider(), Options.Create(new BrasilApiOptions { PersistenceBatchSize = 1 }));

        await processor.ProcessAsync(fixture.ImportId, job.Id, CancellationToken.None);

        Assert.Equal(99m, job.ProgressPercent);
        Assert.Contains("3/3 CNPJs", job.ProgressMessage);
        Assert.Contains("3 resolvidos", job.ProgressMessage);
        Assert.Contains("\"processed\":3", job.ResultJson);
        Assert.Contains("\"pending\":0", job.ResultJson);
    }

    [Theory]
    [InlineData(0, 0, 99)]
    [InlineData(0, 10, 0)]
    [InlineData(5, 10, 49.5)]
    [InlineData(10, 10, 99)]
    [InlineData(20, 10, 99)]
    public void CalculateProgressPercent_RespectsFormulaAndBounds(
        int processed, int total, decimal expected)
    {
        Assert.Equal(expected,
            CustomerRegistrationAddressEnrichmentProcessor.CalculateProgressPercent(processed, total));
    }

    private static CustomerRegistrationAddressEnrichmentProcessor CreateProcessor(
        ImportDbContext db, ICustomerRegistrationAddressProvider provider) =>
        new(db, provider, Options.Create(new BrasilApiOptions { PersistenceBatchSize = 25 }));

    private static ImportDbContext CreateDb() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"customer-addresses-{Guid.NewGuid()}").Options);

    private static async Task<Fixture> SeedAsync(ImportDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot,
            NextImportVersion = 2, Active = true, CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "clientes.xlsx",
            FilePath = "clientes.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
        };
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(), StateCode = "SP", Name = "Marília",
            NormalizedName = "MARILIA", CreatedAt = now
        };
        var customers = Enumerable.Range(1, 4).Select(index => new Customer
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, BranchCode = "01",
            ExternalCode = index.ToString("0000"), CreatedAt = now
        }).ToArray();
        db.AddRange(source, import, municipality);
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();
        db.CustomerSnapshots.AddRange(
            Snapshot(import.Id, customers[0].Id, municipality.Id, "11111111000111", "CNPJ"),
            Snapshot(import.Id, customers[1].Id, municipality.Id, "22222222000122", "CNPJ"),
            Snapshot(import.Id, customers[2].Id, municipality.Id, "33333333000133", "CNPJ"),
            Snapshot(import.Id, customers[3].Id, municipality.Id, "11111111111", "CPF"));
        await db.SaveChangesAsync();
        return new Fixture(import.Id, customers[1].Id, customers[2].Id, customers[3].Id);
    }

    private static CustomerSnapshot Snapshot(
        Guid importId, Guid customerId, Guid municipalityId, string document, string type) => new()
    {
        Id = Guid.NewGuid(), ImportId = importId, CustomerId = customerId,
        MunicipalityId = municipalityId, DocumentNumber = document, DocumentType = type,
        LegalName = "Cliente", TradeName = "Cliente", CustomerType = "Mercado",
        SourceRowNumber = 1, CreatedAt = DateTime.UtcNow
    };

    private sealed record Fixture(Guid ImportId, Guid FailedCustomerId, Guid ResolvedCustomerId, Guid CpfCustomerId);

    private sealed class RecordingProvider : ICustomerRegistrationAddressProvider
    {
        public List<string> Cnpjs { get; } = [];

        public Task<CustomerRegistrationAddressLookup> FindByCnpjAsync(
            string cnpj,
            CancellationToken cancellationToken)
        {
            Cnpjs.Add(cnpj);
            return Task.FromResult(new CustomerRegistrationAddressLookup(
                CustomerRegistrationAddressStatuses.Resolved,
                "17500000", "SP", "MARILIA", "RUA TESTE", "10", null, "CENTRO"));
        }
    }

    private sealed class FailingProvider : ICustomerRegistrationAddressProvider
    {
        public Task<CustomerRegistrationAddressLookup> FindByCnpjAsync(
            string cnpj,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("BrasilAPI indisponível.");
    }

    private sealed class RateLimitedThenSuccessfulProvider : ICustomerRegistrationAddressProvider
    {
        public Task<CustomerRegistrationAddressLookup> FindByCnpjAsync(
            string cnpj, CancellationToken cancellationToken)
        {
            if (cnpj == "11111111000111") throw new BrasilApiRateLimitException(cnpj, 6);
            return Task.FromResult(new CustomerRegistrationAddressLookup(
                CustomerRegistrationAddressStatuses.Resolved,
                "17500000", "SP", "MARILIA", "RUA TESTE", "10", null, "CENTRO"));
        }
    }
}
