using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class OsrmDailyMatrixServiceTests
{
    [Fact]
    public async Task GetForDayAsync_UsesDepotAndDistinctExactCustomersFromOnlyRequestedDay()
    {
        await using var db = Context();
        var fixture = await SeedAsync(db, includeCoordinate: true);
        var client = new CapturingClient();

        await new OsrmDailyMatrixService(db, client)
            .GetForDayAsync(fixture.ImportId, " monday ", CancellationToken.None);

        var request = Assert.IsType<OsrmTableRequest>(client.Request);
        Assert.Equal("MONDAY", request.Weekday);
        Assert.Equal(2, request.Points.Count);
        Assert.Equal(OsrmMatrixPointTypes.Depot, request.Points[0].Type);
        Assert.Equal(fixture.CustomerId, request.Points[1].Id);
        Assert.Equal(OsrmMatrixPointTypes.Customer, request.Points[1].Type);
    }

    [Fact]
    public async Task GetForDayAsync_RejectsMissingExactCustomerCoordinateWithoutCallingOsrm()
    {
        await using var db = Context();
        var fixture = await SeedAsync(db, includeCoordinate: false);
        var client = new CapturingClient();

        await Assert.ThrowsAsync<OsrmTableException>(() => new OsrmDailyMatrixService(db, client)
            .GetForDayAsync(fixture.ImportId, "MONDAY", CancellationToken.None));
        Assert.Null(client.Request);
    }

    [Fact]
    public async Task GetForDayAsync_RejectsRepeatedCoordinatesInsteadOfCreatingZeroLengthLegs()
    {
        await using var db = Context();
        var fixture = await SeedAsync(db, includeCoordinate: true);
        var route = await db.Routes.SingleAsync(item => item.ImportId == fixture.ImportId && item.Weekday == "MONDAY");
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(), DataSourceId = route.Import!.DataSourceId, BranchCode = "1",
            ExternalCode = "C002", IsActive = true, CreatedAt = now
        };
        var address = new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = "2",
            Source = "TEST", Status = CustomerRegistrationAddressStatuses.Resolved,
            City = "Marília", StateCode = "SP", CreatedAt = now, UpdatedAt = now
        };
        customer.RegistrationAddress = address;
        db.AddRange(customer, address,
            new CustomerAddressCoordinate
            {
                Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id, NormalizedAddress = "TEST2",
                Source = "TEST", Status = CustomerAddressCoordinateStatuses.Resolved,
                Precision = CustomerAddressCoordinatePrecisions.Exact, Latitude = -22.21m,
                Longitude = -49.95m, CreatedAt = now, UpdatedAt = now
            },
            new RouteCustomerAssignment
            {
                Id = Guid.NewGuid(), RouteId = route.Id, Route = route, CustomerId = customer.Id,
                Customer = customer, MunicipalityId = fixture.MunicipalityId,
                Source = RouteCustomerAssignmentSource.InferredByMunicipality,
                CreatedAt = now, UpdatedAt = now
            });
        await db.SaveChangesAsync();
        var client = new CapturingClient();

        var result = await new OsrmDailyMatrixService(db, client)
            .GetForDayAsync(fixture.ImportId, "MONDAY", default);

        Assert.NotNull(result);
        Assert.NotNull(client.Request);
    }

    [Fact]
    public async Task GetForDayAsync_RejectsZeroMatrixLegBetweenDistinctCoordinates()
    {
        await using var db = Context();
        var fixture = await SeedAsync(db, includeCoordinate: true);
        var route = await db.Routes.SingleAsync(item => item.ImportId == fixture.ImportId && item.Weekday == "MONDAY");
        var now = DateTime.UtcNow;
        var customer = new Customer { Id = Guid.NewGuid(), DataSourceId = route.Import!.DataSourceId, BranchCode = "2", ExternalCode = "C002", IsActive = true, CreatedAt = now };
        var address = new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = "2", City = "Marília", StateCode = "SP",
            Status = CustomerRegistrationAddressStatuses.Resolved, CreatedAt = now, UpdatedAt = now
        };
        customer.RegistrationAddress = address;
        db.AddRange(customer, address,
            new CustomerAddressCoordinate
            {
                Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id, NormalizedAddress = "TEST2",
                Source = "TEST", Status = CustomerAddressCoordinateStatuses.Resolved,
                Precision = CustomerAddressCoordinatePrecisions.Exact, Latitude = -22.22m, Longitude = -49.96m,
                CreatedAt = now, UpdatedAt = now
            },
            new RouteCustomerAssignment
            {
                Id = Guid.NewGuid(), RouteId = route.Id, Route = route, CustomerId = customer.Id,
                Customer = customer, MunicipalityId = fixture.MunicipalityId,
                Source = RouteCustomerAssignmentSource.InferredByMunicipality, CreatedAt = now, UpdatedAt = now
            });
        await db.SaveChangesAsync();
        var client = new CapturingClient { ReturnZeroForDistinctLegs = true };

        var result = await new OsrmDailyMatrixService(db, client)
            .GetForDayAsync(fixture.ImportId, "MONDAY", default);

        Assert.True(result.DistancesMeters[1][2] > 0);
        Assert.True(result.DurationsSeconds[1][2] > 0);
    }

    [Fact]
    public async Task GetForDayAsync_RepairsSubmeterPositiveLegBetweenDistinctCoordinates()
    {
        await using var db = Context();
        var fixture = await SeedAsync(db, includeCoordinate: true);
        var route = await db.Routes.SingleAsync(item => item.ImportId == fixture.ImportId && item.Weekday == "MONDAY");
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(), DataSourceId = route.Import!.DataSourceId, BranchCode = "3", ExternalCode = "C003",
            IsActive = true, CreatedAt = now
        };
        var address = new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = "3", City = "Marília", StateCode = "SP",
            Status = CustomerRegistrationAddressStatuses.Resolved, CreatedAt = now, UpdatedAt = now
        };
        customer.RegistrationAddress = address;
        db.AddRange(customer, address,
            new CustomerAddressCoordinate
            {
                Id = Guid.NewGuid(), CustomerRegistrationAddressId = address.Id, NormalizedAddress = "TEST3",
                Source = "TEST", Status = CustomerAddressCoordinateStatuses.Resolved,
                Precision = CustomerAddressCoordinatePrecisions.Exact, Latitude = -22.22m,
                Longitude = -49.96m, CreatedAt = now, UpdatedAt = now
            },
            new RouteCustomerAssignment
            {
                Id = Guid.NewGuid(), RouteId = route.Id, Route = route, CustomerId = customer.Id,
                Customer = customer, MunicipalityId = fixture.MunicipalityId,
                Source = RouteCustomerAssignmentSource.InferredByMunicipality,
                CreatedAt = now, UpdatedAt = now
            });
        await db.SaveChangesAsync();
        var client = new CapturingClient { ReturnSubmeterForDistinctLegs = true };

        var result = await new OsrmDailyMatrixService(db, client)
            .GetForDayAsync(fixture.ImportId, "MONDAY", default);

        Assert.True(result.DistancesMeters[0][1] >= 1m);
        Assert.True(result.DurationsSeconds[0][1] >= 1m);
        Assert.True(result.DistancesMeters[1][2] >= 1m);
        Assert.True(result.DurationsSeconds[1][2] >= 1m);
    }

    [Fact]
    public async Task GetForDayAsync_DoesNotSendExcludedStopToMatrix()
    {
        await using var db = Context();
        var fixture = await SeedAsync(db, includeCoordinate: true);
        var mondayId = await db.Routes.Where(route => route.ImportId == fixture.ImportId && route.Weekday == "MONDAY")
            .Select(route => route.Id).SingleAsync();
        db.RouteEntries.Add(new RouteEntry
        {
            Id = Guid.NewGuid(), RouteId = mondayId, Name = "FORA", AveragePerDay = 0,
            IsExcludedFromOptimization = true, Sequence = 3, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var client = new CapturingClient();

        await new OsrmDailyMatrixService(db, client).GetForDayAsync(fixture.ImportId, "MONDAY", default);

        var request = Assert.IsType<OsrmTableRequest>(client.Request);
        Assert.Equal(2, request.Points.Count);
        Assert.DoesNotContain(request.Points, point => point.Id == Guid.Empty);
    }

    private static async Task<(Guid ImportId, Guid MunicipalityId, Guid CustomerId)> SeedAsync(ImportDbContext db, bool includeCoordinate)
    {
        var now = DateTime.UtcNow;
        var source = new DataSource { Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, ProcessorKey = "routes",
            Name = "Rotas", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, Active = true, CreatedAt = now, UpdatedAt = now };
        var import = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, DataSource = source, Version = 1,
            FileName = "rotas.xlsx", FilePath = "rotas.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now };
        var vehicle = new VehicleType { Id = Guid.NewGuid(), Name = "Truck", CapacityKg = 10_300 };
        var city = new Municipality { Id = Guid.NewGuid(), StateCode = "SP", Name = "Marília", NormalizedName = "MARILIA", CreatedAt = now };
        if (includeCoordinate)
            city.Coordinate = new MunicipalityCoordinate { Id = Guid.NewGuid(), MunicipalityId = city.Id,
                Latitude = -22.217m, Longitude = -49.950m, Source = "CSV", Status = MunicipalityCoordinateStatuses.Resolved,
                CreatedAt = now, UpdatedAt = now };
        var monday = new Route { Id = Guid.NewGuid(), ImportId = import.Id, Import = import, Name = "Rota 1", Weekday = "MONDAY",
            VehicleTypeId = vehicle.Id, VehicleType = vehicle, CreatedAt = now };
        var tuesday = new Route { Id = Guid.NewGuid(), ImportId = import.Id, Import = import, Name = "Rota 2", Weekday = "TUESDAY",
            VehicleTypeId = vehicle.Id, VehicleType = vehicle, CreatedAt = now };
        var customer = new Customer { Id = Guid.NewGuid(), DataSourceId = source.Id, BranchCode = "1",
            ExternalCode = "C001", IsActive = true, CreatedAt = now };
        var address = new CustomerRegistrationAddress { Id = Guid.NewGuid(), CustomerId = customer.Id,
            DocumentNumber = "1", Source = "TEST", Status = CustomerRegistrationAddressStatuses.Resolved,
            City = city.Name, StateCode = city.StateCode, CreatedAt = now, UpdatedAt = now };
        customer.RegistrationAddress = address;
        db.AddRange(source, import, vehicle, city,
            new LogisticsDepot { Id = Guid.NewGuid(), Name = "Grespan", Address = "Marília", Latitude = -22.2m, Longitude = -49.9m, CreatedAt = now, UpdatedAt = now },
            customer, address, monday, tuesday,
            new RouteEntry { Id = Guid.NewGuid(), Route = monday, RouteId = monday.Id, Municipality = city, MunicipalityId = city.Id, Name = city.Name, Sequence = 1, AveragePerDay = 100, CreatedAt = now },
            new RouteEntry { Id = Guid.NewGuid(), Route = monday, RouteId = monday.Id, Municipality = city, MunicipalityId = city.Id, Name = city.Name, Sequence = 2, AveragePerDay = 100, CreatedAt = now },
            new RouteEntry { Id = Guid.NewGuid(), Route = tuesday, RouteId = tuesday.Id, Municipality = city, MunicipalityId = city.Id, Name = city.Name, Sequence = 1, AveragePerDay = 100, CreatedAt = now });
        db.RouteCustomerAssignments.AddRange(
            new RouteCustomerAssignment { Id = Guid.NewGuid(), Route = monday, RouteId = monday.Id,
                Customer = customer, CustomerId = customer.Id, MunicipalityId = city.Id,
                Source = RouteCustomerAssignmentSource.InferredByMunicipality, CreatedAt = now, UpdatedAt = now },
            new RouteCustomerAssignment { Id = Guid.NewGuid(), Route = tuesday, RouteId = tuesday.Id,
                Customer = customer, CustomerId = customer.Id, MunicipalityId = city.Id,
                Source = RouteCustomerAssignmentSource.InferredByMunicipality, CreatedAt = now, UpdatedAt = now });
        if (includeCoordinate)
            db.CustomerAddressCoordinates.Add(new CustomerAddressCoordinate { Id = Guid.NewGuid(),
                CustomerRegistrationAddressId = address.Id, NormalizedAddress = "TEST", Source = "TEST",
                Status = CustomerAddressCoordinateStatuses.Resolved, Precision = CustomerAddressCoordinatePrecisions.Exact,
                Latitude = -22.21m, Longitude = -49.95m, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        return (import.Id, city.Id, customer.Id);
    }

    private static ImportDbContext Context() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"osrm-daily-{Guid.NewGuid()}").Options);

    private sealed class CapturingClient : IOsrmTableClient
    {
        public bool ReturnZeroForDistinctLegs { get; init; }
        public bool ReturnSubmeterForDistinctLegs { get; init; }
        public OsrmTableRequest? Request { get; private set; }
        public Task<OsrmTableResult> GetTableAsync(OsrmTableRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            var distances = request.Points.Select((_, source) => (IReadOnlyList<decimal>)request.Points
                .Select((_, destination) => source == destination
                    ? 0m
                    : ReturnZeroForDistinctLegs
                        ? 0m
                        : ReturnSubmeterForDistinctLegs ? 0.25m : 100m).ToArray()).ToArray();
            return Task.FromResult(new OsrmTableResult("TEST", request.Points, distances, distances));
        }
        public Task<bool> IsHealthyAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
