using System.Text.Json;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
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

    private static ImportDbContext CreateDb() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"vehicle-fuel-settings-{Guid.NewGuid()}").Options);
}
