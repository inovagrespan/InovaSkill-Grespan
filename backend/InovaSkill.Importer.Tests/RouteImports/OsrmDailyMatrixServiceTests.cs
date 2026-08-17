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
    public async Task GetForDayAsync_UsesDepotAndDistinctResolvedMunicipalitiesFromOnlyRequestedDay()
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
        Assert.Equal(fixture.MunicipalityId, request.Points[1].Id);
    }

    [Fact]
    public async Task GetForDayAsync_RejectsMissingMunicipalityCoordinateWithoutCallingOsrm()
    {
        await using var db = Context();
        var fixture = await SeedAsync(db, includeCoordinate: false);
        var client = new CapturingClient();

        await Assert.ThrowsAsync<OsrmTableException>(() => new OsrmDailyMatrixService(db, client)
            .GetForDayAsync(fixture.ImportId, "MONDAY", CancellationToken.None));
        Assert.Null(client.Request);
    }

    private static async Task<(Guid ImportId, Guid MunicipalityId)> SeedAsync(ImportDbContext db, bool includeCoordinate)
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
        db.AddRange(source, import, vehicle, city,
            new LogisticsDepot { Id = Guid.NewGuid(), Name = "Grespan", Address = "Marília", Latitude = -22.2m, Longitude = -49.9m, CreatedAt = now, UpdatedAt = now },
            monday, tuesday,
            new RouteEntry { Id = Guid.NewGuid(), Route = monday, RouteId = monday.Id, Municipality = city, MunicipalityId = city.Id, Name = city.Name, Sequence = 1, CreatedAt = now },
            new RouteEntry { Id = Guid.NewGuid(), Route = monday, RouteId = monday.Id, Municipality = city, MunicipalityId = city.Id, Name = city.Name, Sequence = 2, CreatedAt = now },
            new RouteEntry { Id = Guid.NewGuid(), Route = tuesday, RouteId = tuesday.Id, Municipality = city, MunicipalityId = city.Id, Name = city.Name, Sequence = 1, CreatedAt = now });
        await db.SaveChangesAsync();
        return (import.Id, city.Id);
    }

    private static ImportDbContext Context() => new(new DbContextOptionsBuilder<ImportDbContext>()
        .UseInMemoryDatabase($"osrm-daily-{Guid.NewGuid()}").Options);

    private sealed class CapturingClient : IOsrmTableClient
    {
        public OsrmTableRequest? Request { get; private set; }
        public Task<OsrmTableResult> GetTableAsync(OsrmTableRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            var matrix = request.Points.Select(_ => (IReadOnlyList<decimal>)new decimal[request.Points.Count]).ToArray();
            return Task.FromResult(new OsrmTableResult("TEST", request.Points, matrix, matrix));
        }
        public Task<bool> IsHealthyAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
