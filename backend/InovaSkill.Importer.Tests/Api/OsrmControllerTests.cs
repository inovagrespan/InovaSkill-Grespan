using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class OsrmControllerTests
{
    [Fact]
    public async Task Health_RequiresConfiguredDepot()
    {
        await using var db = Context();
        var result = await new OsrmController(db, new HealthClient(true)).Health(CancellationToken.None);
        Assert.Equal(503, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 503)]
    public async Task Health_ReportsOsrmResult(bool healthy, int status)
    {
        await using var db = Context();
        db.LogisticsDepots.Add(new LogisticsDepot { Id = Guid.NewGuid(), Name = "Grespan", Address = "Marília",
            Latitude = -22.2m, Longitude = -49.9m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await new OsrmController(db, new HealthClient(healthy)).Health(CancellationToken.None);
        if (status == 200) Assert.IsType<OkObjectResult>(result);
        else Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    private static ImportDbContext Context() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"osrm-controller-{Guid.NewGuid()}").Options);

    private sealed class HealthClient(bool healthy) : IOsrmTableClient
    {
        public Task<bool> IsHealthyAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken) => Task.FromResult(healthy);
        public Task<OsrmTableResult> GetTableAsync(OsrmTableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
