using System.Text.Json;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class RoutesControllerTests
{
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
            CreateRoute(routeImport.Id, vehicle.Id, "Crítico", 1.0001m),
            CreateRoute(routeImport.Id, vehicle.Id, "Saudável", 1.00m),
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

    private static ImportDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

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
}
