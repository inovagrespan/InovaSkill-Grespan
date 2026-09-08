using System.Text.Json;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace InovaSkill.Importer.Tests.Api;

public sealed class RoutesControllerTests
{
    [Theory]
    [InlineData(true, RouteOptimizationDecisionStatuses.Approved)]
    [InlineData(false, RouteOptimizationDecisionStatuses.Rejected)]
    public async Task DecideDailyOptimization_RecordsAuditableDecisionWithoutChangingRoutes(bool approved, string expectedStatus)
    {
        await using var db = CreateDbContext();
        var user = new AppUser { Id = 7, Name = "Logística", Email = "logistica@test.com", PasswordHash = "hash", Role = AppUserRoles.Logistica };
        var job = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization,
            ParametersJson = "{}", ResultJson = "{}", Status = JobExecutionStatus.Completed,
            RelatedEntityId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow
        };
        db.AddRange(user, job);
        await db.SaveChangesAsync();
        var controller = new RoutesController(db) { ControllerContext = AuthenticatedContext(user) };

        var response = await controller.DecideDailyOptimization(job.Id, new DecideDailyOptimizationRequest(approved, "Análise operacional"), default);

        Assert.IsType<OkObjectResult>(response);
        var decision = await db.RouteOptimizationDecisions.SingleAsync();
        Assert.Equal(expectedStatus, decision.Status);
        Assert.Equal(user.Id, decision.DecidedByUserId);
        Assert.Equal("Análise operacional", decision.Justification);
        Assert.NotNull(decision.DecidedAt);
    }

    [Fact]
    public async Task DecideDailyOptimization_RejectsSecondDecision()
    {
        await using var db = CreateDbContext();
        var user = new AppUser { Id = 8, Name = "Admin", Email = "admin@test.com", PasswordHash = "hash", Role = AppUserRoles.Admin };
        var job = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, ParametersJson = "{}", ResultJson = "{}", Status = JobExecutionStatus.Completed, RelatedEntityId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        db.AddRange(user, job, new RouteOptimizationDecision { JobExecutionId = job.Id, Status = RouteOptimizationDecisionStatuses.Approved, DecidedByUserId = user.Id, DecidedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = new RoutesController(db) { ControllerContext = AuthenticatedContext(user) };

        var response = await controller.DecideDailyOptimization(job.Id, new DecideDailyOptimizationRequest(false, null), default);

        Assert.IsType<ConflictObjectResult>(response);
        Assert.Equal(RouteOptimizationDecisionStatuses.Approved, (await db.RouteOptimizationDecisions.SingleAsync()).Status);
    }

    [Fact]
    public async Task GetOccupancySummary_UsesCurrentSnapshotAndWeightedCapacityAverage()
    {
        await using var db = CreateDbContext();
        var source = CreateSource();
        var currentImport = CreateImport(source.Id, 2, RouteImportStatus.Completed, new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc));
        var olderImport = CreateImport(source.Id, 1, RouteImportStatus.Completed, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
        source.CurrentImportId = currentImport.Id;
        source.CurrentImport = currentImport;
        var smallVehicle = CreateVehicle(capacityKg: 1_000m);
        var largeVehicle = CreateVehicle(name: "Carreta", capacityKg: 10_000m);
        var missingCapacityVehicle = CreateVehicle(name: "Sem capacidade", capacityKg: null);
        db.AddRange(source, currentImport, olderImport, smallVehicle, largeVehicle, missingCapacityVehicle);
        db.AddRange(
            CreateRoute(currentImport.Id, smallVehicle.Id, "Rota atual A", 0.90m, totalWeightKg: 900m),
            CreateRoute(currentImport.Id, largeVehicle.Id, "Rota atual B", 0.10m, totalWeightKg: 1_000m),
            CreateRoute(currentImport.Id, missingCapacityVehicle.Id, "Rota sem capacidade", null, totalWeightKg: 8_000m),
            CreateRoute(olderImport.Id, largeVehicle.Id, "Rota anterior", 0.80m, totalWeightKg: 8_000m));
        await db.SaveChangesAsync();

        var response = await new RoutesController(db).GetOccupancySummary(default);
        var json = SerializeOkResult(response);

        Assert.Equal(17.3m, json.RootElement.GetProperty("OccupancyRatePercent").GetDecimal());
        Assert.Equal(1_900m, json.RootElement.GetProperty("TotalWeightKg").GetDecimal());
        Assert.Equal(11_000m, json.RootElement.GetProperty("TotalCapacityKg").GetDecimal());
        Assert.Equal(3, json.RootElement.GetProperty("RouteCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("RoutesWithCapacity").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("RoutesWithoutCapacity").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("Snapshot").GetProperty("Version").GetInt64());
        Assert.Equal("2.xlsx", json.RootElement.GetProperty("Snapshot").GetProperty("FileName").GetString());
    }

    [Fact]
    public async Task GetOccupancySummary_WhenCurrentSnapshotDoesNotExist_ReturnsZeroSummary()
    {
        await using var db = CreateDbContext();
        db.Add(CreateSource());
        await db.SaveChangesAsync();

        var response = await new RoutesController(db).GetOccupancySummary(default);
        var json = SerializeOkResult(response);

        Assert.Equal(0m, json.RootElement.GetProperty("OccupancyRatePercent").GetDecimal());
        Assert.Equal(0m, json.RootElement.GetProperty("TotalWeightKg").GetDecimal());
        Assert.Equal(0m, json.RootElement.GetProperty("TotalCapacityKg").GetDecimal());
        Assert.Equal(0, json.RootElement.GetProperty("RouteCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("Snapshot").ValueKind);
    }

    [Fact]
    public async Task GetOccupancySummary_PreservesOverCapacityResult()
    {
        await using var db = CreateDbContext();
        var source = CreateSource();
        var currentImport = CreateImport(source.Id, 1, RouteImportStatus.Completed, DateTime.UtcNow);
        source.CurrentImportId = currentImport.Id;
        source.CurrentImport = currentImport;
        var vehicle = CreateVehicle(capacityKg: 1_000m);
        db.AddRange(source, currentImport, vehicle);
        db.Add(CreateRoute(currentImport.Id, vehicle.Id, "Rota acima da capacidade", 1m, totalWeightKg: 1_250m));
        await db.SaveChangesAsync();

        var response = await new RoutesController(db).GetOccupancySummary(default);
        var json = SerializeOkResult(response);

        Assert.Equal(125m, json.RootElement.GetProperty("OccupancyRatePercent").GetDecimal());
    }

    [Fact]
    public async Task List_WithArbitraryDate_SelectsLatestSnapshotAvailableByEndOfDay()
    {
        await using var db = CreateDbContext();
        var source = CreateSource();
        var older = CreateImport(source.Id, 1, RouteImportStatus.Completed, new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc));
        var selected = CreateImport(source.Id, 2, RouteImportStatus.Completed, new DateTime(2026, 7, 6, 1, 0, 0, DateTimeKind.Utc));
        var future = CreateImport(source.Id, 3, RouteImportStatus.Completed, new DateTime(2026, 7, 6, 4, 0, 0, DateTimeKind.Utc));
        var review = CreateImport(source.Id, 4, RouteImportStatus.NeedsReview, new DateTime(2026, 7, 5, 22, 0, 0, DateTimeKind.Utc));
        var vehicle = CreateVehicle();
        db.AddRange(source, older, selected, future, review, vehicle);
        db.AddRange(
            CreateRoute(older.Id, vehicle.Id, "Anterior", 0.50m),
            CreateRoute(selected.Id, vehicle.Id, "Selecionada", 0.90m),
            CreateRoute(future.Id, vehicle.Id, "Futura", 1.10m),
            CreateRoute(review.Id, vehicle.Id, "Em revisão", 1.20m));
        await db.SaveChangesAsync();

        var response = await new RoutesController(db).List(
            date: new DateOnly(2026, 7, 5),
            cancellationToken: default);
        var json = SerializeOkResult(response);

        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(
            "Selecionada",
            json.RootElement.GetProperty("items")[0].GetProperty("Name").GetString());
    }

    [Theory]
    [InlineData("critical", "Crítico")]
    [InlineData("good", "Saudável")]
    [InlineData("medium", "Médio")]
    [InlineData("idle", "Ocioso")]
    [InlineData("unavailable", "Indisponível")]
    public async Task List_FiltersEveryOccupancyLevelBeforePagination(
        string occupancyLevel,
        string expectedRoute)
    {
        await using var db = CreateDbContext();
        var source = CreateSource();
        var routeImport = CreateImport(source.Id, 1, RouteImportStatus.Completed, new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc));
        source.CurrentImportId = routeImport.Id;
        var vehicle = CreateVehicle();
        db.AddRange(source, routeImport, vehicle);
        db.AddRange(
            CreateRoute(routeImport.Id, vehicle.Id, "Crítico", 0.9501m),
            CreateRoute(routeImport.Id, vehicle.Id, "Saudável", 0.95m),
            CreateRoute(routeImport.Id, vehicle.Id, "Médio", 0.60m),
            CreateRoute(routeImport.Id, vehicle.Id, "Ocioso", 0.5999m),
            CreateRoute(routeImport.Id, vehicle.Id, "Indisponível", null));
        await db.SaveChangesAsync();

        var response = await new RoutesController(db).List(
            pageSize: 1,
            occupancyLevel: occupancyLevel,
            cancellationToken: default);
        var json = SerializeOkResult(response);

        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(
            expectedRoute,
            json.RootElement.GetProperty("items")[0].GetProperty("Name").GetString());
    }

    [Fact]
    public async Task List_WithUnknownOccupancyLevel_ReturnsBadRequest()
    {
        await using var db = CreateDbContext();

        var response = await new RoutesController(db).List(
            occupancyLevel: "urgent",
            cancellationToken: default);

        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Theory]
    [InlineData("rota interior", "Rota Interior")]
    [InlineData("bady bassitt", "Rota Interior")]
    [InlineData("BÁDY BASSÍTT", "Rota Interior")]
    public async Task List_SearchesRouteNameOrCityInBackendBeforePagination(
        string search,
        string expectedRoute)
    {
        await using var db = CreateDbContext();
        var source = CreateSource();
        var routeImport = CreateImport(
            source.Id, 1, RouteImportStatus.Completed,
            new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc));
        source.CurrentImportId = routeImport.Id;
        var vehicle = CreateVehicle();
        var matchingRoute = CreateRoute(routeImport.Id, vehicle.Id, expectedRoute, 0.80m);
        matchingRoute.Entries =
        [
            new RouteEntry
            {
                Id = Guid.NewGuid(),
                RouteId = matchingRoute.Id,
                Sequence = 1,
                Name = "BADY BASSITT",
                CreatedAt = DateTime.UtcNow
            }
        ];
        db.AddRange(source, routeImport, vehicle, matchingRoute);
        db.Add(CreateRoute(routeImport.Id, vehicle.Id, "Outra rota", 0.80m));
        await db.SaveChangesAsync();

        var response = await new RoutesController(db).List(
            page: 1,
            pageSize: 1,
            search: search,
            cancellationToken: default);
        var json = SerializeOkResult(response);

        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(
            expectedRoute,
            json.RootElement.GetProperty("items")[0].GetProperty("Name").GetString());
    }

    [Fact]
    public async Task GetRoadPath_UsesOnlyActiveCustomersWithExactCoordinatesAndReturnsOsrmGeometry()
    {
        await using var db = CreateDbContext();
        var source = CreateSource();
        var routeImport = CreateImport(source.Id, 1, RouteImportStatus.Completed, DateTime.UtcNow);
        var vehicle = CreateVehicle();
        var route = CreateRoute(routeImport.Id, vehicle.Id, "Marília", 0.8m);
        var municipality = new Municipality { Id = Guid.NewGuid(), Name = "MARILIA", StateCode = "SP", CreatedAt = DateTime.UtcNow };
        route.Entries = [new RouteEntry { Id = Guid.NewGuid(), RouteId = route.Id, Sequence = 1, Name = municipality.Name, MunicipalityId = municipality.Id, CreatedAt = DateTime.UtcNow }];
        var customer = new Customer { Id = Guid.NewGuid(), DataSourceId = source.Id, ExternalCode = "10", IsActive = true, CreatedAt = DateTime.UtcNow };
        customer.Snapshots = [new CustomerSnapshot { Id = Guid.NewGuid(), ImportId = routeImport.Id, CustomerId = customer.Id, LegalName = "Cliente Exato", MunicipalityId = municipality.Id, SourceRowNumber = 2, CreatedAt = DateTime.UtcNow }];
        customer.RegistrationAddress = new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            Coordinate = new CustomerAddressCoordinate
            {
                Id = Guid.NewGuid(), Status = CustomerAddressCoordinateStatuses.Resolved,
                Precision = CustomerAddressCoordinatePrecisions.Exact, Latitude = -22.22m, Longitude = -49.94m,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        };
        var depot = new LogisticsDepot { Id = Guid.NewGuid(), Name = "Matriz Grespan", Address = "Marília", Latitude = -22.21m, Longitude = -49.95m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var assignment = new RouteCustomerAssignment { Id = Guid.NewGuid(), RouteId = route.Id, CustomerId = customer.Id, MunicipalityId = municipality.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.AddRange(source, routeImport, vehicle, municipality, route, customer, depot, assignment);
        await db.SaveChangesAsync();
        var osrm = new CapturingRouteClient();

        var response = await new RoutesController(db, osrm).GetRoadPath(route.Id, default);
        var json = SerializeOkResult(response);

        Assert.Equal(3, osrm.Points!.Count);
        Assert.Equal(depot.Id, osrm.Points[0].Id);
        Assert.Equal(customer.Id, osrm.Points[1].Id);
        Assert.Equal(depot.Id, osrm.Points[2].Id);
        Assert.Equal("OSRM_ROUTE_DRIVING", json.RootElement.GetProperty("Source").GetString());
        var geometry = json.RootElement.GetProperty("geometry");
        Assert.Equal("LineString", geometry.GetProperty("type").GetString());
        Assert.Equal(2, geometry.GetProperty("coordinates").GetArrayLength());
    }

    [Fact]
    public async Task GetOptimizedRoadPath_UsesSavedExactCustomerCoordinatesInProposedMunicipalityOrder()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var source = CreateSource();
        var routeImport = CreateImport(source.Id, 1, RouteImportStatus.Completed, now);
        var vehicle = CreateVehicle();
        var route = CreateRoute(routeImport.Id, vehicle.Id, "Original", 0.8m);
        var first = new Municipality { Id = Guid.NewGuid(), Name = "Marília", NormalizedName = "MARILIA", StateCode = "SP", CreatedAt = now };
        var second = new Municipality { Id = Guid.NewGuid(), Name = "Bauru", NormalizedName = "BAURU", StateCode = "SP", CreatedAt = now };
        Customer ExactCustomer(string code, string name, Guid municipalityId, decimal latitude, decimal longitude)
        {
            var customer = new Customer { Id = Guid.NewGuid(), DataSourceId = source.Id, ExternalCode = code, IsActive = true, CreatedAt = now };
            customer.Snapshots = [new CustomerSnapshot { Id = Guid.NewGuid(), ImportId = routeImport.Id, CustomerId = customer.Id,
                LegalName = name, MunicipalityId = municipalityId, SourceRowNumber = 1, CreatedAt = now }];
            customer.RegistrationAddress = new CustomerRegistrationAddress { Id = Guid.NewGuid(), CustomerId = customer.Id,
                CreatedAt = now, UpdatedAt = now, Coordinate = new CustomerAddressCoordinate { Id = Guid.NewGuid(),
                    Status = CustomerAddressCoordinateStatuses.Resolved, Precision = CustomerAddressCoordinatePrecisions.Exact,
                    Latitude = latitude, Longitude = longitude, CreatedAt = now, UpdatedAt = now } };
            return customer;
        }
        var mariliaCustomer = ExactCustomer("20", "Cliente Marília", first.Id, -22.21m, -49.95m);
        var bauruCustomer = ExactCustomer("10", "Cliente Bauru", second.Id, -22.32m, -49.06m);
        var depot = new LogisticsDepot { Id = Guid.NewGuid(), Name = "Matriz Grespan", Address = "Marília",
            Latitude = -22.20m, Longitude = -49.90m, CreatedAt = now, UpdatedAt = now };
        db.AddRange(source, routeImport, vehicle, route, first, second, depot, mariliaCustomer, bauruCustomer,
            new RouteCustomerAssignment { Id = Guid.NewGuid(), RouteId = route.Id, CustomerId = mariliaCustomer.Id,
                MunicipalityId = first.Id, CreatedAt = now, UpdatedAt = now },
            new RouteCustomerAssignment { Id = Guid.NewGuid(), RouteId = route.Id, CustomerId = bauruCustomer.Id,
                MunicipalityId = second.Id, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        var client = new CapturingRouteClient();

        var response = await new RoutesController(db, client).GetOptimizedRoadPath(
            new OptimizedRoadPathRequest(routeImport.Id, "Nova rota", [second.Id, first.Id]), default);
        var json = SerializeOkResult(response);

        Assert.Equal(4, client.Points!.Count);
        Assert.Equal([depot.Id, bauruCustomer.Id, mariliaCustomer.Id, depot.Id], client.Points.Select(point => point.Id));
        Assert.Equal(-22.32m, client.Points[1].Latitude);
        Assert.Contains("Cliente Bauru", client.Points[1].Label);
        Assert.Equal("Nova rota", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(4, json.RootElement.GetProperty("stops").GetArrayLength());
        Assert.Equal(second.Id, json.RootElement.GetProperty("stops")[1].GetProperty("municipalityId").GetGuid());
        Assert.Equal(first.Id, json.RootElement.GetProperty("stops")[2].GetProperty("municipalityId").GetGuid());
        Assert.Equal("LineString", json.RootElement.GetProperty("geometry").GetProperty("type").GetString());
    }

    private static ImportDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ControllerContext AuthenticatedContext(AppUser user) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            ], "test"))
        }
    };

    private static DataSource CreateSource() => new()
    {
        Id = Guid.NewGuid(),
        Code = RouteImportCodes.DataSource,
        ProcessorKey = "routes-by-city",
        Name = "Rotas",
        Type = "XLSX",
        ImportMode = DataSourceImportMode.Snapshot,
        NextImportVersion = 1,
        Active = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static RouteImport CreateImport(
        Guid sourceId,
        long version,
        RouteImportStatus status,
        DateTime finishedAt) => new()
    {
        Id = Guid.NewGuid(),
        DataSourceId = sourceId,
        Version = version,
        FileName = $"{version}.xlsx",
        FilePath = version.ToString(),
        Status = status,
        CreatedAt = finishedAt.AddMinutes(-5),
        FinishedAt = finishedAt
    };

    private static VehicleType CreateVehicle(
        string name = "Truck",
        decimal? capacityKg = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CapacityKg = capacityKg
    };

    private static Route CreateRoute(
        Guid importId,
        Guid vehicleId,
        string name,
        decimal? occupancy,
        decimal totalWeightKg = 0m) => new()
    {
        Id = Guid.NewGuid(),
        ImportId = importId,
        Name = name,
        Weekday = "MONDAY",
        VehicleTypeId = vehicleId,
        TotalWeightKg = totalWeightKg,
        OverallOccupancy = occupancy,
        OccupancyStatus = occupancy.HasValue
            ? RouteOccupancyStatus.Calculated
            : RouteOccupancyStatus.MissingCapacity,
        CreatedAt = DateTime.UtcNow
    };

    private static JsonDocument SerializeOkResult(ActionResult response)
    {
        var ok = Assert.IsType<OkObjectResult>(response);
        return JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    }

    private sealed class CapturingRouteClient : IRouteGeometryClient
    {
        public IReadOnlyList<RouteGeometryPoint>? Points { get; private set; }

        public Task<RouteGeometryResult> GetRouteAsync(IReadOnlyList<RouteGeometryPoint> points, CancellationToken cancellationToken)
        {
            Points = points;
            IReadOnlyList<IReadOnlyList<decimal>> geometry =
            [
                [points[0].Longitude, points[0].Latitude],
                [points[1].Longitude, points[1].Latitude]
            ];
            return Task.FromResult(new RouteGeometryResult("OSRM_ROUTE_DRIVING", points, geometry, 1000m, 120m));
        }
    }
}
