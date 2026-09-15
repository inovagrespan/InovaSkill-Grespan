using System.Text.Json;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class VehicleFuelSettingsControllerTests
{
    [Fact]
    public async Task GetFuelSettings_CreatesDefaultSettingsWhenMissing()
    {
        await using var db = CreateDb();
        var controller = new VehicleTypesController(db);

        var response = await controller.GetFuelSettings(CancellationToken.None);

        var payload = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(response).Value);
        Assert.Equal(LogisticsFuelSettings.DefaultDieselPricePerLiter,
            payload.GetProperty("DieselPricePerLiter").GetDecimal());
        Assert.Single(db.LogisticsFuelSettings);
    }

    [Fact]
    public async Task UpdateFuelSettings_RoundsAndPersistsPositivePrice()
    {
        await using var db = CreateDb();
        var controller = new VehicleTypesController(db);

        var response = await controller.UpdateFuelSettings(new UpdateFuelSettingsRequest(6.9876m), CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        Assert.Equal(6.988m, (await db.LogisticsFuelSettings.SingleAsync()).DieselPricePerLiter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateFuelSettings_RejectsNonPositivePrice(decimal price)
    {
        await using var db = CreateDb();
        var controller = new VehicleTypesController(db);

        var response = await controller.UpdateFuelSettings(new UpdateFuelSettingsRequest(price), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Empty(db.LogisticsFuelSettings);
    }

    [Fact]
    public async Task CreateVehicleType_PersistsCompleteCostConfiguration()
    {
        await using var db = CreateDb();
        var controller = new VehicleTypesController(db);

        var response = await controller.Create(
            new CreateVehicleTypeRequest("Truck 6x2", 10_300m, 3, 3.2m, 4m),
            default);

        Assert.IsType<CreatedAtActionResult>(response);
        var vehicle = await db.VehicleTypes.SingleAsync();
        Assert.Equal(3, vehicle.AxleCount);
        Assert.Equal(3.2m, vehicle.MinimumFuelEfficiencyKmPerLiter);
        Assert.Equal(4m, vehicle.MaximumFuelEfficiencyKmPerLiter);
    }

    [Fact]
    public async Task CreateVehicleType_RejectsInvertedEfficiencyRange()
    {
        await using var db = CreateDb();
        var controller = new VehicleTypesController(db);

        var response = await controller.Create(
            new CreateVehicleTypeRequest("Inválido", 1_000m, 2, 7m, 5m),
            default);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Empty(db.VehicleTypes);
    }

    [Fact]
    public async Task UpdateFuelSettings_QueuesCostConsolidationForCurrentRouteSnapshot()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, Name = "Rotas", ProcessorKey = "routes",
            Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot, Active = true, CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "rotas.xlsx",
            FilePath = "rotas.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
        };
        source.CurrentImportId = import.Id;
        db.AddRange(source, import);
        await db.SaveChangesAsync();
        var queue = new RecordingJobQueue();
        var controller = new VehicleTypesController(db, queue);

        await controller.UpdateFuelSettings(new UpdateFuelSettingsRequest(7m), default);

        Assert.Equal(OperationalJobCodes.RouteCostConsolidation, queue.JobType);
        Assert.Equal(import.Id, queue.RelatedEntityId);
    }

    private static ImportDbContext CreateDb() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"vehicle-fuel-settings-{Guid.NewGuid()}").Options);

    private sealed class RecordingJobQueue : IOperationalJobQueue
    {
        public string? JobType { get; private set; }
        public Guid? RelatedEntityId { get; private set; }

        public Task<Guid?> TryQueueAsync(string jobType, Guid relatedEntityId, CancellationToken cancellationToken)
        {
            JobType = jobType;
            RelatedEntityId = relatedEntityId;
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }
}
