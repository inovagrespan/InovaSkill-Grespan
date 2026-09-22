using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class DailyRouteOptimizationProcessorTests
{
    [Fact]
    public async Task ProcessAsync_UsesEveryExactCustomerLocationAndPreservesMunicipalLoad()
    {
        await using var db = Context();
        var fixture = await Seed(db);
        var sourceId = await db.DataSources.Select(source => source.Id).FirstAsync();
        var city = await db.Municipalities.SingleAsync(item => item.Name == "Marília");
        var secondCustomer = CustomerWithExactCoordinate(sourceId, "C003", city, DateTime.UtcNow);
        var mondayRoutes = await db.Routes.Where(route => route.Weekday == "MONDAY").ToListAsync();
        db.Customers.Add(secondCustomer);
        db.RouteCustomerAssignments.AddRange(mondayRoutes.Select(route =>
            Assignment(route.Id, secondCustomer, city.Id, DateTime.UtcNow)));
        await db.SaveChangesAsync();
        var solver = new CapturingSolver();

        await new DailyRouteOptimizationProcessor(db, new FakeMatrixService(db), solver)
            .ProcessAsync(fixture.ImportId, fixture.JobId, default);

        var problem = Assert.Single(solver.Problems, item => item.Weekday == "MONDAY");
        Assert.Equal(2, problem.Blocks.Count);
        Assert.Equal(3_000_000, problem.Blocks.Sum(block => block.WeightGrams));
        Assert.Equal(["C001", "C003"], problem.Blocks.Select(block => block.Name).Order().ToArray());
        Assert.All(problem.Matrix.Points.Skip(1), point => Assert.Equal(OsrmMatrixPointTypes.Customer, point.Type));
        Assert.Equal(problem.Blocks.Select(block => block.CustomerId).Order(),
            problem.Matrix.Points.Skip(1).Select(point => point.Id).Order());
    }

    [Fact]
    public async Task ProcessAsync_SplitsOversizedCityAcrossMultipleTruckLoads()
    {
        await using var db = Context();
        var fixture = await Seed(db);
        var mondayEntries = await db.RouteEntries
            .Where(entry => entry.Route!.Weekday == "MONDAY")
            .OrderBy(entry => entry.Id)
            .ToListAsync();
        mondayEntries[0].AveragePerDay = 6_000;
        mondayEntries[1].AveragePerDay = 5_000;
        await db.SaveChangesAsync();

        var solver = new CapturingSolver();
        await new DailyRouteOptimizationProcessor(
                db, new FakeMatrixService(db), solver)
            .ProcessAsync(fixture.ImportId, fixture.JobId, default);

        var monday = Assert.Single(solver.Problems, problem => problem.Weekday == "MONDAY");
        Assert.Equal([700_000L, 10_300_000L], monday.Blocks.Select(block => block.WeightGrams).Order().ToArray());
        Assert.Single(monday.Blocks.Select(block => block.MunicipalityId).Distinct());
        Assert.Equal(11_000_000, monday.Blocks.Sum(block => block.WeightGrams));
    }

    [Fact]
    public async Task ProcessAsync_IgnoresStopsWithoutCustomers()
    {
        await using var db = Context();
        var fixture = await Seed(db);
        var orphanCity = City("Olímpia", DateTime.UtcNow);
        var monday = await db.Routes.FirstAsync(route => route.Weekday == "MONDAY");
        db.Municipalities.Add(orphanCity);
        db.RouteEntries.Add(Entry(monday.Id, orphanCity, 9_999, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var solver = new CapturingSolver();

        await new DailyRouteOptimizationProcessor(db, new FakeMatrixService(db), solver)
            .ProcessAsync(fixture.ImportId, fixture.JobId, default);

        var problem = Assert.Single(solver.Problems, item => item.Weekday == "MONDAY");
        Assert.Equal(3_000_000, problem.Blocks.Sum(block => block.WeightGrams));
        var result = await db.DailyRouteOptimizationResults.SingleAsync(item => item.Weekday == "MONDAY");
        Assert.NotEqual(DailyRouteOptimizationStatuses.InsufficientData, result.Status);
    }

    [Fact]
    public async Task ProcessAsync_PersistsAllMissingLinksWithoutMutatingSnapshotAndKeepsOtherDayRunnable()
    {
        await using var db = Context();
        var fixture = await Seed(db);
        var mondayEntries = await db.RouteEntries
            .Where(entry => entry.Route!.Weekday == "MONDAY")
            .ToListAsync();
        foreach (var entry in mondayEntries)
        {
            entry.MunicipalityId = null;
            entry.Municipality = null;
        }
        await db.SaveChangesAsync();
        var matrix = new FakeMatrixService(db);
        var solver = new CapturingSolver();

        await new DailyRouteOptimizationProcessor(db, matrix, solver)
            .ProcessAsync(fixture.ImportId, fixture.JobId, default);

        var problem = Assert.Single(solver.Problems);
        Assert.Equal("TUESDAY", problem.Weekday);
        Assert.All(mondayEntries, entry => Assert.Null(entry.MunicipalityId));
        Assert.Equal(2, await db.DailyRouteOptimizationResults.CountAsync());
        var monday = await db.DailyRouteOptimizationResults.Include(result => result.Issues)
            .SingleAsync(result => result.Weekday == "MONDAY");
        Assert.Equal(DailyRouteOptimizationStatuses.InsufficientData, monday.Status);
        Assert.Equal(2, monday.Issues.Count);
        Assert.All(monday.Issues, issue => Assert.Equal(DailyRouteOptimizationIssueCodes.MunicipalityNotLinked, issue.Code));
        var tuesday = await db.DailyRouteOptimizationResults.SingleAsync(result => result.Weekday == "TUESDAY");
        Assert.Equal(DailyRouteOptimizationStatuses.NoImprovement, tuesday.Status);
    }

    [Fact]
    public async Task ProcessAsync_CountsOnlyVehiclesWithStopsInProposedFleetMetrics()
    {
        await using var db = Context();
        var fixture = await Seed(db);
        var mondayEntries = await db.RouteEntries
            .Where(entry => entry.Route!.Weekday == "MONDAY")
            .OrderBy(entry => entry.Id)
            .ToListAsync();
        mondayEntries[0].AveragePerDay = 6_000;
        mondayEntries[1].AveragePerDay = 5_000;
        var mondayRoutes = await db.Routes.Where(route => route.Weekday == "MONDAY").ToListAsync();
        mondayRoutes[0].TotalWeightKg = 20_000;
        await db.SaveChangesAsync();

        await new DailyRouteOptimizationProcessor(
                db, new FakeMatrixService(db), new SolutionWithIdleVehicleSolver())
            .ProcessAsync(fixture.ImportId, fixture.JobId, default);

        var result = await db.DailyRouteOptimizationResults
            .Include(item => item.Vehicles)
            .SingleAsync(item => item.Weekday == "MONDAY");
        Assert.Equal(DailyRouteOptimizationStatuses.Optimized, result.Status);
        Assert.Equal(3, result.Vehicles.Count);
        Assert.Single(result.Vehicles, vehicle => vehicle.IsIdle);
        Assert.Equal(2, result.ProposedVehicleCount);
        Assert.Equal(1, result.AdditionalVehicleCount);
        Assert.Equal(3_300m, result.AdditionalCapacityKg);
    }

    [Fact]
    public async Task ProcessAsync_PreservesPreviousDayResultWhenSolverFailsTechnically()
    {
        await using var db = Context();
        var fixture = await Seed(db);
        var previous = new DailyRouteOptimizationResult
        {
            Id = Guid.NewGuid(), RouteImportId = fixture.ImportId, JobExecutionId = fixture.JobId,
            Weekday = "MONDAY", Status = DailyRouteOptimizationStatuses.Optimized,
            CurrentDistanceMeters = 2_000, ProposedDistanceMeters = 1_500,
            CurrentVehicleCount = 2, ProposedVehicleCount = 2, TotalWeightKg = 3_000,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        db.DailyRouteOptimizationResults.Add(previous);
        await db.SaveChangesAsync();

        var processor = new DailyRouteOptimizationProcessor(
            db, new FakeMatrixService(db), new ThrowingSolver());

        await Assert.ThrowsAsync<TimeoutException>(() =>
            processor.ProcessAsync(fixture.ImportId, fixture.JobId, default));
        var persisted = await db.DailyRouteOptimizationResults.AsNoTracking().SingleAsync();
        Assert.Equal(previous.Id, persisted.Id);
        Assert.Equal(1_500, persisted.ProposedDistanceMeters);
    }

    private static async Task<(Guid ImportId, Guid JobId)> Seed(ImportDbContext db)
    {
        var now = DateTime.UtcNow;
        var source = new DataSource { Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, ProcessorKey = "routes", Name = "Rotas", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, Active = true, CreatedAt = now, UpdatedAt = now };
        var import = new RouteImport { Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "routes.xlsx", FilePath = "routes.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now };
        var truck = new VehicleType { Id = Guid.NewGuid(), Name = "Truck", CapacityKg = 10_300 };
        var acelo = new VehicleType { Id = Guid.NewGuid(), Name = "Acelo", CapacityKg = 3_300 };
        var mondayCity = City("Marília", now); var tuesdayCity = City("Bauru", now);
        var mondayA = Route(import.Id, truck.Id, "MONDAY", "A", now); var mondayB = Route(import.Id, truck.Id, "MONDAY", "B", now); var tuesday = Route(import.Id, acelo.Id, "TUESDAY", "C", now);
        mondayA.VehicleCapacityKgSnapshot = truck.CapacityKg!.Value;
        mondayB.VehicleCapacityKgSnapshot = truck.CapacityKg.Value;
        tuesday.VehicleCapacityKgSnapshot = acelo.CapacityKg!.Value;
        mondayA.Entries.Add(Entry(mondayA.Id, mondayCity, 1_000, now));
        mondayB.Entries.Add(Entry(mondayB.Id, mondayCity, 2_000, now));
        tuesday.Entries.Add(Entry(tuesday.Id, tuesdayCity, 500, now));
        var mondayCustomer = CustomerWithExactCoordinate(source.Id, "C001", mondayCity, now);
        var tuesdayCustomer = CustomerWithExactCoordinate(source.Id, "C002", tuesdayCity, now);
        mondayA.CustomerAssignments.Add(Assignment(mondayA.Id, mondayCustomer, mondayCity.Id, now));
        mondayB.CustomerAssignments.Add(Assignment(mondayB.Id, mondayCustomer, mondayCity.Id, now));
        tuesday.CustomerAssignments.Add(Assignment(tuesday.Id, tuesdayCustomer, tuesdayCity.Id, now));
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, RelatedEntityId = import.Id, Status = JobExecutionStatus.Processing, CreatedAt = now };
        db.AddRange(source, import, truck, acelo, mondayCity, tuesdayCity, mondayCustomer,
            tuesdayCustomer, mondayA, mondayB, tuesday, job);
        await db.SaveChangesAsync();
        return (import.Id, job.Id);
    }

    private static Municipality City(string name, DateTime now)
    {
        var city = new Municipality { Id = Guid.NewGuid(), Name = name, NormalizedName = MunicipalityNameNormalizer.Normalize(name), StateCode = "SP", CreatedAt = now };
        city.Coordinate = new MunicipalityCoordinate { Id = Guid.NewGuid(), MunicipalityId = city.Id, Status = MunicipalityCoordinateStatuses.Resolved, Source = "TEST", Latitude = -22, Longitude = -49, CreatedAt = now, UpdatedAt = now };
        return city;
    }
    private static Route Route(Guid importId, Guid typeId, string day, string name, DateTime now) => new() { Id = Guid.NewGuid(), ImportId = importId, VehicleTypeId = typeId, Weekday = day, Name = name, CreatedAt = now };
    private static RouteEntry Entry(Guid routeId, Municipality city, decimal weight, DateTime now) => new() { Id = Guid.NewGuid(), RouteId = routeId, Municipality = city, MunicipalityId = city.Id, Name = city.Name, AveragePerDay = weight, CreatedAt = now };
    private static Customer CustomerWithExactCoordinate(Guid dataSourceId, string code, Municipality city, DateTime now)
    {
        var customer = new Customer { Id = Guid.NewGuid(), DataSourceId = dataSourceId, BranchCode = "1",
            ExternalCode = code, IsActive = true, CreatedAt = now };
        customer.RegistrationAddress = new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = code, Source = "TEST",
            Status = CustomerRegistrationAddressStatuses.Resolved, City = city.Name, StateCode = city.StateCode,
            CreatedAt = now, UpdatedAt = now
        };
        customer.RegistrationAddress.Coordinate = new CustomerAddressCoordinate
        {
            Id = Guid.NewGuid(), CustomerRegistrationAddressId = customer.RegistrationAddress.Id,
            NormalizedAddress = code, Source = "TEST", Status = CustomerAddressCoordinateStatuses.Resolved,
            Precision = CustomerAddressCoordinatePrecisions.Exact, Latitude = -22, Longitude = -49,
            CreatedAt = now, UpdatedAt = now
        };
        return customer;
    }
    private static RouteCustomerAssignment Assignment(Guid routeId, Customer customer, Guid municipalityId, DateTime now) =>
        new() { Id = Guid.NewGuid(), RouteId = routeId, CustomerId = customer.Id, Customer = customer,
            MunicipalityId = municipalityId, Source = RouteCustomerAssignmentSource.InferredByMunicipality,
            CreatedAt = now, UpdatedAt = now };
    private static ImportDbContext Context() => new(new DbContextOptionsBuilder<ImportDbContext>().UseInMemoryDatabase($"daily-optimization-{Guid.NewGuid()}").Options);

    private sealed class CapturingSolver : IDailyRouteOptimizationSolver
    {
        public List<RouteOptimizationProblem> Problems { get; } = [];
        public RouteOptimizationSolution Solve(RouteOptimizationProblem problem)
        {
            Problems.Add(problem);
            return new(DailyRouteOptimizationStatuses.NoImprovement, "A distribuição atual foi preservada.", [], 0, 0);
        }
    }

    private sealed class ThrowingSolver : IDailyRouteOptimizationSolver
    {
        public RouteOptimizationSolution Solve(RouteOptimizationProblem problem) =>
            throw new TimeoutException("Falha técnica simulada.");
    }

    private sealed class SolutionWithIdleVehicleSolver : IDailyRouteOptimizationSolver
    {
        public RouteOptimizationSolution Solve(RouteOptimizationProblem problem)
        {
            if (problem.Weekday != "MONDAY")
                return new(
                    DailyRouteOptimizationStatuses.NoImprovement,
                    "A distribuição atual foi preservada.",
                    [],
                    0,
                    0);
            var fullLoadIndex = Array.FindIndex(problem.Blocks.ToArray(), block => block.WeightGrams == 10_300_000);
            var remainderIndex = Array.FindIndex(problem.Blocks.ToArray(), block => block.WeightGrams == 700_000);
            var additional = problem.AdditionalVehicleTypes.Single(vehicle => vehicle.VehicleTypeName == "Acelo");
            return new RouteOptimizationSolution(
                DailyRouteOptimizationStatuses.Optimized,
                null,
                [
                    new(problem.ExistingVehicles[0], [], 0, 0, 0),
                    new(problem.ExistingVehicles[1], [new(fullLoadIndex, 0, 0)], 10_300_000, 0, 0),
                    new(additional, [new(remainderIndex, 0, 0)], 700_000, 0, 0)
                ],
                0,
                0);
        }
    }

    private sealed class FakeMatrixService(ImportDbContext db) : IOsrmDailyMatrixService
    {
        public async Task<OsrmTableResult> GetForDayAsync(Guid importId, string weekday, CancellationToken cancellationToken)
        {
            var ids = await db.RouteCustomerAssignments
                .Where(assignment => assignment.Route!.ImportId == importId && assignment.Route.Weekday == weekday &&
                    assignment.Route.Entries.Any(entry => !entry.IsExcludedFromOptimization &&
                        entry.MunicipalityId == assignment.MunicipalityId))
                .Select(assignment => assignment.CustomerId).Distinct().ToListAsync(cancellationToken);
            var points = new[] { new OsrmMatrixPoint(Guid.NewGuid(), OsrmMatrixPointTypes.Depot, 0, 0) }
                .Concat(ids.Select(id => new OsrmMatrixPoint(id, OsrmMatrixPointTypes.Customer, 0, 0))).ToArray();
            var values = points.Select(_ => (IReadOnlyList<decimal>)new decimal[points.Length]).ToArray();
            return new("TEST", points, values, values);
        }
    }
}
