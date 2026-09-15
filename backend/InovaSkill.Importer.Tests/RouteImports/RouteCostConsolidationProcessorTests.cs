using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RouteCostConsolidationProcessorTests
{
    [Fact]
    public async Task ProcessAsync_PersistsExactTotalsAndReusesUnchangedFingerprint()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"route-cost-{Guid.NewGuid()}").Options);
        var fixture = await Seed(db);
        var geometry = new FakeGeometryClient();
        var processor = new RouteCostConsolidationProcessor(db, geometry, new FakeTollCatalog());

        await processor.ProcessAsync(fixture.ImportId, fixture.FirstJobId, default);
        await processor.ProcessAsync(fixture.ImportId, fixture.SecondJobId, default);

        var snapshot = Assert.Single(await db.RouteCostSnapshots.Include(item => item.Items)
            .ThenInclude(item => item.TollPassageItems).ToListAsync());
        var actual = Assert.Single(snapshot.Items, item => item.Scenario == RouteCostScenarios.Actual);
        Assert.True(actual.IsAvailable);
        Assert.Equal(RouteCostPathBases.ExactCustomers, actual.PathBasis);
        Assert.Equal(100_000m, actual.DistanceMeters);
        Assert.Equal(14.286m, actual.MinimumFuelLiters);
        Assert.Equal(18.182m, actual.MaximumFuelLiters);
        Assert.Equal(98.57m, actual.MinimumFuelCost);
        Assert.Equal(125.46m, actual.MaximumFuelCost);
        Assert.Equal(25.84m, actual.TollCost);
        Assert.Equal(actual.MinimumFuelCost + actual.TollCost, actual.MinimumTotalCost);
        Assert.Equal(actual.MaximumFuelCost + actual.TollCost, actual.MaximumTotalCost);
        Assert.Single(actual.TollPassageItems);
        Assert.Equal(1, geometry.Calls);
        Assert.Contains("\"reused\":true", (await db.JobExecutions.FindAsync(fixture.SecondJobId))!.ResultJson);
    }

    [Fact]
    public async Task ProcessAsync_TechnicalGeometryFailure_PreservesLastValidSnapshot()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"route-cost-failure-{Guid.NewGuid()}").Options);
        var fixture = await Seed(db);
        var catalog = new FakeTollCatalog();
        await new RouteCostConsolidationProcessor(db, new FakeGeometryClient(), catalog)
            .ProcessAsync(fixture.ImportId, fixture.FirstJobId, default);
        var validSnapshotId = (await db.RouteCostSnapshots.SingleAsync()).Id;
        (await db.LogisticsFuelSettings.SingleAsync()).DieselPricePerLiter = 7.10m;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<RouteGeometryException>(() =>
            new RouteCostConsolidationProcessor(db, new FailingGeometryClient(), catalog)
                .ProcessAsync(fixture.ImportId, fixture.SecondJobId, default));

        Assert.Equal(validSnapshotId, (await db.RouteCostSnapshots.SingleAsync()).Id);
    }

    [Fact]
    public async Task ProcessAsync_WithoutDiesel_PersistsExplicitUnavailabilityWithoutCallingGeometry()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"route-cost-no-diesel-{Guid.NewGuid()}").Options);
        var fixture = await Seed(db);
        db.LogisticsFuelSettings.Remove(await db.LogisticsFuelSettings.SingleAsync());
        await db.SaveChangesAsync();
        var geometry = new FakeGeometryClient();

        await new RouteCostConsolidationProcessor(db, geometry, new FakeTollCatalog())
            .ProcessAsync(fixture.ImportId, fixture.FirstJobId, default);

        var snapshot = await db.RouteCostSnapshots.Include(item => item.Items).SingleAsync();
        Assert.Null(snapshot.DieselPricePerLiter);
        Assert.All(snapshot.Items, item => Assert.False(item.IsAvailable));
        Assert.Contains(snapshot.Items, item => item.UnavailableReason == "O preço do diesel não está configurado.");
        Assert.Equal(0, geometry.Calls);
    }

    private static async Task<(Guid ImportId, Guid FirstJobId, Guid SecondJobId)> Seed(ImportDbContext db)
    {
        var importId = Guid.NewGuid();
        var firstJobId = Guid.NewGuid();
        var secondJobId = Guid.NewGuid();
        var vehicle = new VehicleType
        {
            Id = Guid.NewGuid(), Name = "Accelo", CapacityKg = 5000,
            AxleCount = 2, MinimumFuelEfficiencyKmPerLiter = 5.5m,
            MaximumFuelEfficiencyKmPerLiter = 7m
        };
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(), Name = "Marília", NormalizedName = "MARILIA", StateCode = "SP"
        };
        var customer = new Customer
        {
            Id = Guid.NewGuid(), DataSourceId = Guid.NewGuid(), BranchCode = "01", ExternalCode = "100",
            RegistrationAddress = new CustomerRegistrationAddress
            {
                Id = Guid.NewGuid(), DocumentNumber = "1", Source = "TEST",
                Coordinate = new CustomerAddressCoordinate
                {
                    Id = Guid.NewGuid(), NormalizedAddress = "RUA A", Source = "TEST",
                    Status = CustomerAddressCoordinateStatuses.Resolved,
                    Precision = CustomerAddressCoordinatePrecisions.Exact,
                    Latitude = -22.21m, Longitude = -49.95m
                }
            }
        };
        var route = new Route
        {
            Id = Guid.NewGuid(), ImportId = importId, Name = "Marília 1", Weekday = "MONDAY",
            VehicleTypeId = vehicle.Id, VehicleType = vehicle, VehicleCapacityKgSnapshot = 5000,
            Entries = [new RouteEntry { Id = Guid.NewGuid(), Sequence = 1, Name = "Marília", MunicipalityId = municipality.Id, Municipality = municipality }]
        };
        route.CustomerAssignments.Add(new RouteCustomerAssignment
        {
            Id = Guid.NewGuid(), RouteId = route.Id, CustomerId = customer.Id, Customer = customer,
            MunicipalityId = municipality.Id, Municipality = municipality
        });
        db.RouteImports.Add(new RouteImport { Id = importId, DataSourceId = Guid.NewGuid(), FileName = "routes.xlsx", FilePath = "routes.xlsx" });
        db.Routes.Add(route);
        db.LogisticsFuelSettings.Add(new LogisticsFuelSettings { Id = Guid.NewGuid(), DieselPricePerLiter = 6.90m });
        db.LogisticsDepots.Add(new LogisticsDepot { Id = Guid.NewGuid(), Name = "CD", Address = "Marília", Latitude = -22.22m, Longitude = -49.94m });
        db.JobExecutions.AddRange(
            new JobExecution { Id = firstJobId, JobType = OperationalJobCodes.RouteCostConsolidation, Queue = "default", ParametersJson = "{}", RelatedEntityId = importId },
            new JobExecution { Id = secondJobId, JobType = OperationalJobCodes.RouteCostConsolidation, Queue = "default", ParametersJson = "{}", RelatedEntityId = importId });
        await db.SaveChangesAsync();
        return (importId, firstJobId, secondJobId);
    }

    private sealed class FakeGeometryClient : IRouteGeometryClient
    {
        public int Calls { get; private set; }
        public Task<RouteGeometryResult> GetRouteAsync(IReadOnlyList<RouteGeometryPoint> points, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new RouteGeometryResult("TEST", points, [[-49.94m, -22.22m], [-49.95m, -22.21m]], 100_000m, 7_200m));
        }
    }

    private sealed class FailingGeometryClient : IRouteGeometryClient
    {
        public Task<RouteGeometryResult> GetRouteAsync(IReadOnlyList<RouteGeometryPoint> points,
            CancellationToken cancellationToken) =>
            throw new RouteGeometryException("Serviço rodoviário indisponível.");
    }

    private sealed class FakeTollCatalog : ITollCatalog
    {
        private readonly TollPlazaDefinition plaza = new("P", "Praça", "Concessionária", "SP-000", 1, "Marília", -22, -49, .05m,
            new Dictionary<int, decimal> { [2] = 27.20m });
        public TollCatalogDefinition Current => new("test-v1", null, [plaza]);
        public RouteTollEstimate Estimate(IReadOnlyList<IReadOnlyList<decimal>> geometry, int axleCount) =>
            new([new TollPassageEstimate(plaza, axleCount, 1, 25.84m, 25.84m)], 1, 25.84m);
    }
}
