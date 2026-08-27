using System.Text.Json;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class LogisticsDepotControllerTests
{
    [Fact]
    public async Task Upsert_CreatesAndUpdatesSingleDepot()
    {
        await using var db = Context();
        var controller = new LogisticsDepotController(db);

        Assert.IsType<OkObjectResult>(await controller.Upsert(
            new("Grespan", "Avenida Teste, 100", -22.217m, -49.950m), CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.Upsert(
            new("CD Grespan", "Avenida Teste, 200", -22.218m, -49.951m), CancellationToken.None));

        var depot = Assert.Single(db.LogisticsDepots);
        Assert.Equal("CD Grespan", depot.Name);
        Assert.Equal(-22.218m, depot.Latitude);
    }

    [Theory]
    [InlineData("", "Endereço", -22, -49)]
    [InlineData("Depósito", "", -22, -49)]
    [InlineData("Depósito", "Endereço", -91, -49)]
    [InlineData("Depósito", "Endereço", -22, 181)]
    public async Task Upsert_RejectsInvalidInput(string name, string address, double latitude, double longitude)
    {
        await using var db = Context();
        var result = await new LogisticsDepotController(db).Upsert(
            new(name, address, (decimal)latitude, (decimal)longitude), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.LogisticsDepots);
    }

    [Fact]
    public async Task Get_ReturnsNotFoundBeforeConfiguration()
    {
        await using var db = Context();
        Assert.IsType<NotFoundObjectResult>(await new LogisticsDepotController(db).Get(CancellationToken.None));
    }

    private static ImportDbContext Context() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"logistics-depot-{Guid.NewGuid()}").Options);
}
