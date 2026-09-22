using System.Text.Json;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class RouteCostsControllerTests
{
    [Fact]
    public async Task List_SeparatesActualAndOptimizedAndKeepsPartTotalConsistency()
    {
        await using var db = Context();
        var seed = await SeedAsync(db);

        var response = await new RouteCostsController(db).List(null, "MONDAY", default);
        var json = Json(Assert.IsType<OkObjectResult>(response).Value);

        Assert.Equal(seed.CostSnapshotId, json.GetProperty("snapshotId").GetGuid());
        var actual = json.GetProperty("actual");
        Assert.Equal(RouteCostPathBases.ExactCustomers, actual.GetProperty("pathBasis").GetString());
        Assert.Equal(2, actual.GetProperty("totals").GetProperty("itemCount").GetInt32());
        Assert.Equal(1, actual.GetProperty("totals").GetProperty("unavailableItemCount").GetInt32());
        Assert.Equal(110m, actual.GetProperty("totals").GetProperty("minimumTotalCost").GetDecimal());
        Assert.Equal(130m, actual.GetProperty("totals").GetProperty("maximumTotalCost").GetDecimal());
        var optimized = json.GetProperty("optimized");
        Assert.Equal(RouteCostPathBases.OptimizedMunicipalities, optimized.GetProperty("pathBasis").GetString());
        Assert.Equal(90m, optimized.GetProperty("totals").GetProperty("minimumTotalCost").GetDecimal());
        Assert.True(json.GetProperty("optimizationResults")[0].GetProperty("hasConsolidatedCosts").GetBoolean());
    }

    [Fact]
    public async Task GetRouteCost_ReturnsTollBreakdownAndCalculationReferences()
    {
        await using var db = Context();
        var seed = await SeedAsync(db);

        var response = await new RouteCostsController(db).GetRouteCost(seed.RouteId, default);
        var json = Json(Assert.IsType<OkObjectResult>(response).Value);

        Assert.Equal(6.90m, json.GetProperty("dieselPricePerLiter").GetDecimal());
        Assert.Equal("2026-06", json.GetProperty("tollCatalogVersion").GetString());
        var item = json.GetProperty("item");
        Assert.Equal(10m, item.GetProperty("tollCost").GetDecimal());
        Assert.Equal("Praça Marília", item.GetProperty("tolls")[0].GetProperty("tollPlazaName").GetString());
        Assert.Equal(110m, item.GetProperty("minimumTotalCost").GetDecimal());
    }

    [Fact]
    public async Task List_WithInvalidWeekday_ReturnsBadRequest()
    {
        await using var db = Context();

        var response = await new RouteCostsController(db).List(null, "FERIADO", default);

        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Fact]
    public async Task List_WithMixedPathBases_ReturnsMixedInsteadOfFailing()
    {
        await using var db = Context();
        var seed = await SeedAsync(db);
        var snapshot = await db.RouteCostSnapshots.SingleAsync(item => item.Id == seed.CostSnapshotId);
        db.RouteCostItems.Add(new RouteCostItem
        {
            Id = Guid.NewGuid(), SnapshotId = snapshot.Id, Scenario = RouteCostScenarios.Optimized,
            Weekday = "MONDAY", Label = "Sugestão 2", PathBasis = RouteCostPathBases.OptimizedCustomers,
            IsAvailable = true, DistanceMeters = 10_000, DurationSeconds = 900,
            MinimumFuelCost = 20, MaximumFuelCost = 25, TollCost = 0,
            MinimumTotalCost = 20, MaximumTotalCost = 25
        });
        await db.SaveChangesAsync();

        var response = await new RouteCostsController(db).List(null, "MONDAY", default);
        var json = Json(Assert.IsType<OkObjectResult>(response).Value);

        Assert.Equal(RouteCostPathBases.Mixed, json.GetProperty("optimized").GetProperty("pathBasis").GetString());
    }

    [Fact]
    public void TollPlazas_ReturnsVersionedCatalogWithoutInventingEffectiveDate()
    {
        var controller = new TollPlazasController(new FakeTollCatalog());

        var json = Json(Assert.IsType<OkObjectResult>(controller.Get()).Value);

        Assert.Equal("test-v1", json.GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("effectiveFrom").ValueKind);
        Assert.Equal(3, json.GetProperty("items")[0].GetProperty("commercialManualTariffsByAxle")
            .GetProperty("3").GetDecimal());
    }

    private static async Task<(Guid RouteId, Guid CostSnapshotId)> SeedAsync(ImportDbContext db)
    {
        var now = DateTime.UtcNow;
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, ProcessorKey = "routes", Name = "Rotas",
            Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, Active = true, CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "rotas.xlsx",
            FilePath = "rotas.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now, FinishedAt = now
        };
        source.CurrentImportId = import.Id;
        var vehicleType = new VehicleType
        {
            Id = Guid.NewGuid(), Name = "Truck", CapacityKg = 10_300, AxleCount = 3,
            MinimumFuelEfficiencyKmPerLiter = 3.2m, MaximumFuelEfficiencyKmPerLiter = 4m
        };
        var route = new InovaSkill.Importer.Domain.Entities.Route
        {
            Id = Guid.NewGuid(), ImportId = import.Id, Name = "Marília 1", Weekday = "MONDAY",
            VehicleTypeId = vehicleType.Id, VehicleCapacityKgSnapshot = 10_300, CreatedAt = now
        };
        var job = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = OperationalJobCodes.RouteCostConsolidation, Queue = "default",
            ParametersJson = "{}", RelatedEntityId = import.Id, Status = JobExecutionStatus.Completed, CreatedAt = now
        };
        var optimizationJob = new JobExecution
        {
            Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, Queue = "default",
            ParametersJson = "{}", RelatedEntityId = import.Id, Status = JobExecutionStatus.Completed, CreatedAt = now
        };
        var optimization = new DailyRouteOptimizationResult
        {
            Id = Guid.NewGuid(), RouteImportId = import.Id, JobExecutionId = optimizationJob.Id,
            Weekday = "MONDAY", Status = DailyRouteOptimizationStatuses.Optimized, CreatedAt = now
        };
        var snapshot = new RouteCostSnapshot
        {
            Id = Guid.NewGuid(), RouteImportId = import.Id, JobExecutionId = job.Id,
            InputFingerprint = "test", DieselPricePerLiter = 6.90m, TollCatalogVersion = "2026-06",
            CalculatedAt = now
        };
        snapshot.Items.Add(new RouteCostItem
        {
            Id = Guid.NewGuid(), SnapshotId = snapshot.Id, Scenario = RouteCostScenarios.Actual,
            Weekday = "MONDAY", RouteId = route.Id, VehicleTypeId = vehicleType.Id, Label = route.Name,
            PathBasis = RouteCostPathBases.ExactCustomers, IsAvailable = true, DistanceMeters = 50_000,
            DurationSeconds = 3_600, MinimumFuelLiters = 10, MaximumFuelLiters = 12,
            MinimumFuelCost = 100, MaximumFuelCost = 120, TollCost = 10, TollPassages = 1,
            MinimumTotalCost = 110, MaximumTotalCost = 130,
            TollPassageItems =
            [
                new RouteCostTollPassage
                {
                    Id = Guid.NewGuid(), TollPlazaCode = "P1", TollPlazaName = "Praça Marília",
                    OperatorName = "Operadora", Highway = "SP-000", Kilometer = 1, AxleCount = 3,
                    Passages = 1, AutomaticUnitTariff = 10, TotalCost = 10
                }
            ]
        });
        snapshot.Items.Add(new RouteCostItem
        {
            Id = Guid.NewGuid(), SnapshotId = snapshot.Id, Scenario = RouteCostScenarios.Actual,
            Weekday = "MONDAY", Label = "Sem dados", PathBasis = RouteCostPathBases.ExactCustomers,
            IsAvailable = false, UnavailableReason = "Sem coordenadas."
        });
        snapshot.Items.Add(new RouteCostItem
        {
            Id = Guid.NewGuid(), SnapshotId = snapshot.Id, Scenario = RouteCostScenarios.Optimized,
            Weekday = "MONDAY", OptimizationResultId = optimization.Id, Label = "Sugestão 1",
            PathBasis = RouteCostPathBases.OptimizedMunicipalities, IsAvailable = true,
            DistanceMeters = 40_000, DurationSeconds = 3_000, MinimumFuelCost = 80,
            MaximumFuelCost = 100, TollCost = 10, TollPassages = 1,
            MinimumTotalCost = 90, MaximumTotalCost = 110
        });
        db.AddRange(source, import, vehicleType, route, job, optimizationJob, optimization, snapshot);
        await db.SaveChangesAsync();
        return (route.Id, snapshot.Id);
    }

    private static ImportDbContext Context() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"route-cost-controller-{Guid.NewGuid()}").Options);

    private static JsonElement Json(object? value) =>
        JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private sealed class FakeTollCatalog : ITollCatalog
    {
        public TollCatalogDefinition Current { get; } = new(
            "test-v1", null,
            [new("P1", "Praça", "Operadora", "SP-000", 1, "Marília", -22, -49, 0.05m,
                new Dictionary<int, decimal> { [3] = 3m })]);

        public RouteTollEstimate Estimate(IReadOnlyList<IReadOnlyList<decimal>> geometry, int axleCount) =>
            new([], 0, 0);
    }
}
