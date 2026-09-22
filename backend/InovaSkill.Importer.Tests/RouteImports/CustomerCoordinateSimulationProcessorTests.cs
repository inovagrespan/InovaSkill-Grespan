using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class CustomerCoordinateSimulationProcessorTests
{
    [Theory]
    [InlineData("{}", true)]
    [InlineData("{\"recalculateDependents\":true}", true)]
    [InlineData("{\"recalculateDependents\":false}", false)]
    public void ReadRecalculateDependents_UsesExplicitChoiceWithCompatibleDefault(
        string json,
        bool expected)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(expected,
            CustomerCoordinateSimulationProcessor.ReadRecalculateDependents(document.RootElement));
    }

    [Fact]
    public async Task ApplyAndRevert_PreserveExactCoordinatesAndRestoreApproximation()
    {
        await using var db = CreateDb();
        var fixture = await Seed(db);
        var applyJob = Job(fixture.ImportId, "{\"action\":\"APPLY\"}", fixture.UserId);
        db.JobExecutions.Add(applyJob);
        await db.SaveChangesAsync();
        var provider = new RecordingProvider(new(-22.201m, -49.901m, "place-1", "Rua Real, 10", 150m,
            GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource));
        var processor = new CustomerCoordinateSimulationProcessor(db, provider);

        await processor.ProcessAsync(fixture.ImportId, applyJob.Id, default);

        db.ChangeTracker.Clear();
        var applied = await db.CustomerAddressCoordinates.SingleAsync();
        Assert.Equal(CustomerAddressCoordinatePrecisions.Exact, applied.Precision);
        Assert.Equal(GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource, applied.Source);
        Assert.Equal(-22.201m, applied.Latitude);
        var audit = await db.CustomerCoordinateSimulationAudits.SingleAsync();
        Assert.Equal("GEOAPIFY_STREET", audit.OriginalSource);
        Assert.Equal(-22.2m, audit.OriginalLatitude);
        Assert.Equal(150m, audit.DistanceMeters);
        Assert.Equal(GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource, audit.SimulatedSource);
        Assert.Contains("\"applied\":1", applyJob.ResultJson);

        var revertJob = Job(fixture.ImportId,
            $"{{\"action\":\"REVERT\",\"sourceJobExecutionId\":\"{applyJob.Id}\"}}", fixture.UserId);
        db.JobExecutions.Add(revertJob);
        await db.SaveChangesAsync();
        await processor.ProcessAsync(fixture.ImportId, revertJob.Id, default);

        db.ChangeTracker.Clear();
        var restored = await db.CustomerAddressCoordinates.SingleAsync();
        Assert.Equal("GEOAPIFY_STREET", restored.Source);
        Assert.Equal(AddressCoordinateMatchLevels.Street, restored.Precision);
        Assert.Equal(-22.2m, restored.Latitude);
        Assert.NotNull((await db.CustomerCoordinateSimulationAudits.SingleAsync()).RevertedAt);
    }

    [Fact]
    public async Task Apply_TreatsPostalCoordinateMarkedExactAsApproximate()
    {
        await using var db = CreateDb();
        var fixture = await Seed(db);
        var coordinate = await db.CustomerAddressCoordinates.SingleAsync();
        coordinate.Source = "BRASIL_API_POSTAL_CODE";
        coordinate.Precision = CustomerAddressCoordinatePrecisions.Exact;
        var job = Job(fixture.ImportId, "{\"action\":\"APPLY\"}", fixture.UserId);
        db.JobExecutions.Add(job);
        await db.SaveChangesAsync();

        await new CustomerCoordinateSimulationProcessor(db,
            new RecordingProvider(new(-22.201m, -49.901m, "place-postal", "Rua Real, 10", 150m,
                GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource)))
            .ProcessAsync(fixture.ImportId, job.Id, default);

        Assert.Equal(GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource,
            (await db.CustomerAddressCoordinates.SingleAsync()).Source);
        Assert.Contains("\"applied\":1", job.ResultJson);
    }

    [Fact]
    public async Task Apply_LeavesCustomerPendingWhenThereIsNoBaseCoordinate()
    {
        await using var db = CreateDb();
        var fixture = await Seed(db, includeBaseCoordinate: false);
        var job = Job(fixture.ImportId, "{\"action\":\"APPLY\"}", fixture.UserId);
        db.JobExecutions.Add(job);
        await db.SaveChangesAsync();
        var provider = new RecordingProvider(new(-22.201m, -49.901m, "place-1", "Rua Real, 10", 150m,
            GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource));

        await new CustomerCoordinateSimulationProcessor(db, provider)
            .ProcessAsync(fixture.ImportId, job.Id, default);

        Assert.Empty(db.CustomerCoordinateSimulationAudits);
        Assert.Empty(db.CustomerAddressCoordinates);
        Assert.Equal(0, provider.Calls);
        Assert.Contains("\"withoutBase\":1", job.ResultJson);
    }

    [Fact]
    public async Task Apply_RetriesMunicipalityBaseWhenApproximateCoordinateHasNoCompatibleBuilding()
    {
        await using var db = CreateDb();
        var fixture = await Seed(db);
        var coordinate = await db.CustomerAddressCoordinates.SingleAsync();
        coordinate.Latitude = -23m;
        coordinate.Longitude = -50m;
        var job = Job(fixture.ImportId, "{\"action\":\"APPLY\"}", fixture.UserId);
        db.JobExecutions.Add(job);
        await db.SaveChangesAsync();
        var provider = new FallbackRecordingProvider(new(-22.201m, -49.901m, "place-1", "Rua Real, 10", 150m,
            GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource));

        await new CustomerCoordinateSimulationProcessor(db, provider)
            .ProcessAsync(fixture.ImportId, job.Id, default);

        Assert.Equal(2, provider.Points.Count);
        Assert.Equal((-23m, -50m), provider.Points[0]);
        Assert.Equal((-22.2m, -49.9m), provider.Points[1]);
        Assert.Equal("MUNICIPALITY_FALLBACK",
            (await db.CustomerCoordinateSimulationAudits.SingleAsync()).BaseSource);
    }

    [Fact]
    public async Task Revert_DoesNotOverwriteCoordinateChangedAfterSimulation()
    {
        await using var db = CreateDb();
        var fixture = await Seed(db);
        var applyJob = Job(fixture.ImportId, "{\"action\":\"APPLY\"}", fixture.UserId);
        db.JobExecutions.Add(applyJob);
        await db.SaveChangesAsync();
        var processor = new CustomerCoordinateSimulationProcessor(db,
            new RecordingProvider(new(-22.201m, -49.901m, "place-1", "Rua Real, 10", 150m,
                GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource)));
        await processor.ProcessAsync(fixture.ImportId, applyJob.Id, default);
        var coordinate = await db.CustomerAddressCoordinates.SingleAsync();
        coordinate.Source = "HERE_IMPORT";
        coordinate.Latitude = -22.3m;
        await db.SaveChangesAsync();
        var revertJob = Job(fixture.ImportId,
            $"{{\"action\":\"REVERT\",\"sourceJobExecutionId\":\"{applyJob.Id}\"}}", fixture.UserId);
        db.JobExecutions.Add(revertJob);
        await db.SaveChangesAsync();

        await processor.ProcessAsync(fixture.ImportId, revertJob.Id, default);

        Assert.Equal("HERE_IMPORT", coordinate.Source);
        Assert.Equal(-22.3m, coordinate.Latitude);
        Assert.Null((await db.CustomerCoordinateSimulationAudits.SingleAsync()).RevertedAt);
        Assert.Contains("\"conflicts\":1", revertJob.ResultJson);
    }

    [Fact]
    public async Task ApplyAndRevert_CreatesAndRemovesTechnicalAddressWhenRegistrationAddressIsMissing()
    {
        await using var db = CreateDb();
        var fixture = await Seed(db, includeRegistrationAddress: false);
        var applyJob = Job(fixture.ImportId, "{\"action\":\"APPLY\"}", fixture.UserId);
        db.JobExecutions.Add(applyJob);
        await db.SaveChangesAsync();
        var processor = new CustomerCoordinateSimulationProcessor(db,
            new RecordingProvider(new(-22.201m, -49.901m, "place-1", "Rua Real, 10", 150m,
                GeoapifyNearbyBuildingCoordinateProvider.SimulatedSource)));

        await processor.ProcessAsync(fixture.ImportId, applyJob.Id, default);

        var createdAddress = await db.CustomerRegistrationAddresses.SingleAsync();
        Assert.Equal("COORDINATE_SIMULATION", createdAddress.Source);
        Assert.Equal("Marília", createdAddress.City);
        Assert.Null(createdAddress.Street);
        Assert.True((await db.CustomerCoordinateSimulationAudits.SingleAsync())
            .RegistrationAddressCreatedBySimulation);

        var revertJob = Job(fixture.ImportId,
            $"{{\"action\":\"REVERT\",\"sourceJobExecutionId\":\"{applyJob.Id}\"}}", fixture.UserId);
        db.JobExecutions.Add(revertJob);
        await db.SaveChangesAsync();
        await processor.ProcessAsync(fixture.ImportId, revertJob.Id, default);

        Assert.Empty(db.CustomerRegistrationAddresses);
        Assert.Empty(db.CustomerAddressCoordinates);
    }

    private static JobExecution Job(Guid importId, string parameters, long userId) => new()
    {
        Id = Guid.NewGuid(), JobType = OperationalJobCodes.CustomerCoordinateSimulation,
        RelatedEntityId = importId, ParametersJson = parameters, Queue = "default",
        Trigger = JobExecutionTrigger.Manual, Status = JobExecutionStatus.Processing,
        RequestedByUserId = userId, CreatedAt = DateTime.UtcNow
    };

    private static async Task<(Guid ImportId, long UserId)> Seed(
        ImportDbContext db,
        bool includeBaseCoordinate = true,
        bool includeRegistrationAddress = true)
    {
        var now = DateTime.UtcNow;
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, Name = "Clientes", Type = "EXCEL",
            ProcessorKey = "customers", ImportMode = DataSourceImportMode.Snapshot, Active = true,
            CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "clientes.xlsx",
            FilePath = "clientes.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
        };
        source.CurrentImportId = import.Id;
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(), Name = "Marília", NormalizedName = "MARILIA", StateCode = "SP", CreatedAt = now
        };
        if (includeBaseCoordinate)
            municipality.Coordinate = new MunicipalityCoordinate
            {
                Id = Guid.NewGuid(), MunicipalityId = municipality.Id, Latitude = -22.2m, Longitude = -49.9m,
                Source = "IBGE", Status = "RESOLVED", CreatedAt = now, UpdatedAt = now
            };
        var customer = new Customer
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, ExternalCode = "001", BranchCode = "1",
            IsActive = true, CreatedAt = now
        };
        var address = new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = "1", Source = "BRASIL_API",
            Status = CustomerRegistrationAddressStatuses.Resolved, Street = "Rua A", Number = "1",
            City = "Marília", StateCode = "SP", CreatedAt = now
        };
        var snapshot = new CustomerSnapshot
        {
            Id = Guid.NewGuid(), ImportId = import.Id, CustomerId = customer.Id,
            MunicipalityId = municipality.Id, LegalName = "Cliente", TradeName = "Cliente",
            DocumentNumber = "1", CustomerType = "A", CreatedAt = now
        };
        var user = new AppUser
        {
            Id = 99, Name = "Admin", Email = "admin@test.local", Role = AppUserRoles.AdminSystem,
            PasswordHash = "hash", CreatedAt = now
        };
        db.AddRange(source, import, municipality, customer, snapshot, user);
        if (includeRegistrationAddress) db.Add(address);
        if (includeBaseCoordinate && includeRegistrationAddress)
            db.CustomerAddressCoordinates.Add(new CustomerAddressCoordinate
            {
                Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id,
                NormalizedAddress = "RUA A|1|MARILIA", Source = "GEOAPIFY_STREET",
                Status = CustomerAddressCoordinateStatuses.Resolved, Precision = AddressCoordinateMatchLevels.Street,
                Latitude = -22.2m, Longitude = -49.9m, CreatedAt = now, UpdatedAt = now
            });
        await db.SaveChangesAsync();
        return (import.Id, user.Id);
    }

    private static ImportDbContext CreateDb() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"customer-coordinate-simulation-{Guid.NewGuid()}").Options);

    private sealed class RecordingProvider(NearbyBuildingCoordinate? result) : INearbyBuildingCoordinateProvider
    {
        public int Calls { get; private set; }
        public Task<NearbyBuildingCoordinate?> FindAsync(decimal latitude, decimal longitude,
            string city, string stateCode, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FallbackRecordingProvider(NearbyBuildingCoordinate result)
        : INearbyBuildingCoordinateProvider
    {
        public List<(decimal Latitude, decimal Longitude)> Points { get; } = [];

        public Task<NearbyBuildingCoordinate?> FindAsync(decimal latitude, decimal longitude,
            string city, string stateCode, CancellationToken cancellationToken)
        {
            Points.Add((latitude, longitude));
            return Task.FromResult<NearbyBuildingCoordinate?>(Points.Count == 1 ? null : result);
        }
    }
}
