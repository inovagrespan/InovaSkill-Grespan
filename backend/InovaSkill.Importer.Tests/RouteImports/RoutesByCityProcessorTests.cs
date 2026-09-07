using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RoutesByCityProcessorTests
{
    [Fact]
    public async Task ProcessAsync_DerivedSnapshotKeepsParentAndCopiesOnlyUnaffectedResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = CreateSource();
        var parent = CreateImport(source.Id, 1, "same-file");
        parent.Status = RouteImportStatus.Completed;
        var child = CreateImport(source.Id, 2, "same-file");
        child.DerivedFromImportId = parent.Id;
        source.CurrentImportId = null;
        var vehicle = new VehicleType { Id = Guid.NewGuid(), Name = "Truck", CapacityKg = 10_000 };
        var tuesdayVehicle = new VehicleType { Id = Guid.NewGuid(), Name = "Acelo", CapacityKg = 3_300 };
        var user = new AppUser { Id = 1, Name = "logistica", Email = "logistica@test", Role = AppUserRoles.Logistica, PasswordHash = "hash", CreatedAt = now };
        var municipality = new Municipality { Id = Guid.NewGuid(), Name = "MARÍLIA", NormalizedName = "MARILIA", StateCode = "SP", CreatedAt = now };
        municipality.Coordinate = new MunicipalityCoordinate { Id = Guid.NewGuid(), MunicipalityId = municipality.Id, Status = MunicipalityCoordinateStatuses.Resolved, Source = "TEST", Latitude = -22, Longitude = -49, CreatedAt = now, UpdatedAt = now };
        var monday = new Route { Id = Guid.NewGuid(), ImportId = parent.Id, Name = "SEG", Weekday = "MONDAY", VehicleTypeId = vehicle.Id, VehicleCapacityKgSnapshot = 10_000, CreatedAt = now };
        var zeroEntry = new RouteEntry { Id = Guid.NewGuid(), RouteId = monday.Id, Name = municipality.Name, MunicipalityId = municipality.Id, AveragePerDay = 0, CreatedAt = now };
        monday.Entries.Add(zeroEntry);
        var excludedEntry = new RouteEntry { Id = Guid.NewGuid(), RouteId = monday.Id, Sequence = 2, Name = "SEM MUNICÍPIO", AveragePerDay = 0, CreatedAt = now };
        monday.Entries.Add(excludedEntry);
        var aliasEntry = new RouteEntry { Id = Guid.NewGuid(), RouteId = monday.Id, Sequence = 3, Name = "MARÍLIA ALIAS", AveragePerDay = 5, CreatedAt = now };
        monday.Entries.Add(aliasEntry);
        var tuesday = new Route { Id = Guid.NewGuid(), ImportId = parent.Id, Name = "TER", Weekday = "TUESDAY", VehicleTypeId = tuesdayVehicle.Id, VehicleCapacityKgSnapshot = 3_300, TotalWeightKg = 500, CreatedAt = now };
        tuesday.Entries.Add(new RouteEntry { Id = Guid.NewGuid(), RouteId = tuesday.Id, Name = municipality.Name, MunicipalityId = municipality.Id, AveragePerDay = 500, CreatedAt = now });
        var oldJob = new JobExecution { Id = Guid.NewGuid(), JobType = OperationalJobCodes.DailyRouteOptimization, Queue = "default", ParametersJson = "{}", Status = JobExecutionStatus.Completed, RelatedEntityId = parent.Id, CreatedAt = now };
        var oldResult = new DailyRouteOptimizationResult { Id = Guid.NewGuid(), RouteImportId = parent.Id, JobExecutionId = oldJob.Id, Weekday = "TUESDAY", Status = DailyRouteOptimizationStatuses.NoImprovement, TotalWeightKg = 500, CreatedAt = now };
        child.AffectedWeekdays.Add(new RouteImportAffectedWeekday { ImportId = child.Id, Weekday = "MONDAY" });
        db.AddRange(source, parent, child, vehicle, tuesdayVehicle, municipality, user);
        await db.SaveChangesAsync();
        source.CurrentImportId = parent.Id;
        db.AddRange(monday, tuesday, oldJob, oldResult);
        await db.SaveChangesAsync();
        db.RouteImportCorrections.AddRange(
            new RouteImportCorrection { Id = Guid.NewGuid(), DerivedImportId = child.Id, Kind = RouteImportCorrectionKinds.SetWeight, SourceRouteId = monday.Id, SourceRouteEntryId = zeroEntry.Id, OriginalWeightKg = 0, CorrectedWeightKg = 100.5555m, RequestedByUserId = 1, CreatedAt = now },
            new RouteImportCorrection { Id = Guid.NewGuid(), DerivedImportId = child.Id, Kind = RouteImportCorrectionKinds.ExcludeStop, SourceRouteId = monday.Id, SourceRouteEntryId = excludedEntry.Id, OriginalWeightKg = 0, ExcludeFromOptimization = true, RequestedByUserId = 1, CreatedAt = now },
            new RouteImportCorrection { Id = Guid.NewGuid(), DerivedImportId = child.Id, Kind = RouteImportCorrectionKinds.LinkMunicipality, SourceRouteId = monday.Id, SourceRouteEntryId = aliasEntry.Id, SourceLabel = aliasEntry.Name, MunicipalityId = municipality.Id, RequestedByUserId = 1, CreatedAt = now },
            new RouteImportCorrection { Id = Guid.NewGuid(), DerivedImportId = child.Id, Kind = RouteImportCorrectionKinds.SetVehicleTypeCapacity, VehicleTypeId = vehicle.Id, OriginalCapacityKg = 10_000, CorrectedCapacityKg = 12_000, RequestedByUserId = 1, CreatedAt = now });
        await db.SaveChangesAsync();

        var derivedProcessor = new RoutesByCityProcessor(db, new MemoryImportFileStorage([]), new RoutesSpreadsheetParser());
        await derivedProcessor.ProcessAsync(child.Id, default);

        Assert.All(await db.RouteEntries.Where(entry => entry.Route!.ImportId == parent.Id && entry.Route.Weekday == "MONDAY").ToListAsync(), entry => Assert.False(entry.IsExcludedFromOptimization));
        var childMonday = await db.Routes.Include(route => route.Entries).SingleAsync(route => route.ImportId == child.Id && route.Weekday == "MONDAY");
        Assert.Equal(105.556m, childMonday.TotalWeightKg);
        Assert.Equal(100.556m, childMonday.Entries.Single(entry => entry.Name == municipality.Name).AveragePerDay);
        Assert.True(childMonday.Entries.Single(entry => entry.Name == "SEM MUNICÍPIO").IsExcludedFromOptimization);
        Assert.Equal(municipality.Id, childMonday.Entries.Single(entry => entry.Name == aliasEntry.Name).MunicipalityId);
        Assert.Equal(12_000, childMonday.VehicleCapacityKgSnapshot);
        Assert.Equal(10_000, await db.Routes.Where(route => route.Id == monday.Id).Select(route => route.VehicleCapacityKgSnapshot).SingleAsync());
        Assert.Equal(12_000, await db.VehicleTypes.Where(item => item.Id == vehicle.Id).Select(item => item.CapacityKg).SingleAsync());
        var inherited = await db.DailyRouteOptimizationResults.SingleAsync(result => result.RouteImportId == child.Id);
        Assert.Equal("TUESDAY", inherited.Weekday);
        Assert.Equal(oldResult.Id, inherited.InheritedFromResultId);
        Assert.Equal(RouteImportStatus.Completed, child.Status);
        Assert.Equal(parent.FilePath, child.FilePath);
        Assert.Equal(child.Id, source.CurrentImportId);

        var future = CreateImport(source.Id, 3, "future");
        db.RouteImports.Add(future);
        await db.SaveChangesAsync();
        await new RoutesByCityProcessor(db, new MemoryImportFileStorage(CreateWorkbook("MARÍLIA ALIAS")), new RoutesSpreadsheetParser())
            .ProcessAsync(future.Id, default);
        Assert.Equal(municipality.Id, await db.RouteEntries.Where(entry => entry.Route!.ImportId == future.Id)
            .Select(entry => entry.MunicipalityId).SingleAsync());
    }

    [Fact]
    public async Task ProcessAsync_PersistsOverloadAndPreservesOtherImportSnapshots()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection)
            .Options);
        await db.Database.EnsureCreatedAsync();

        var source = CreateSource();
        var historicalImport = CreateImport(source.Id, 1, "historical");
        var candidateImport = CreateImport(source.Id, 2, "candidate");
        var vehicleType = new VehicleType
        {
            Id = Guid.NewGuid(),
            Name = "Truck",
            CapacityKg = 10_000m
        };
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(), StateCode = "SP", Name = "CIDADE A",
            NormalizedName = "CIDADE A", CreatedAt = DateTime.UtcNow
        };
        db.AddRange(source, historicalImport, candidateImport, vehicleType, municipality);
        db.Routes.Add(new Route
        {
            Id = Guid.NewGuid(),
            ImportId = historicalImport.Id,
            Name = "HISTÓRICA",
            Weekday = "MONDAY",
            VehicleTypeId = vehicleType.Id,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var processor = new RoutesByCityProcessor(
            db,
            new MemoryImportFileStorage(CreateWorkbook()),
            new RoutesSpreadsheetParser());

        await processor.ProcessAsync(candidateImport.Id, default);

        var historicalRoute = await db.Routes.SingleAsync(route => route.ImportId == historicalImport.Id);
        var currentRoute = await db.Routes.SingleAsync(route => route.ImportId == candidateImport.Id);
        Assert.Equal("HISTÓRICA", historicalRoute.Name);
        Assert.Equal(12_500m, currentRoute.TotalWeightKg);
        Assert.Equal(1.25m, currentRoute.WeightOccupancy);
        Assert.Equal(1.25m, currentRoute.OverallOccupancy);
        Assert.Equal(RouteOccupancyStatus.Calculated, currentRoute.OccupancyStatus);
        Assert.Equal(
            currentRoute.Entries.Sum(entry => entry.AveragePerDay),
            currentRoute.TotalWeightKg);
        Assert.Equal(municipality.Id, currentRoute.Entries.Single().MunicipalityId);
    }

    private static DataSource CreateSource() => new()
    {
        Id = Guid.NewGuid(),
        Code = RouteImportCodes.DataSource,
        ProcessorKey = RouteImportCodes.ProcessorKey,
        Name = "Rotas",
        Type = "XLSX",
        ImportMode = DataSourceImportMode.Snapshot,
        NextImportVersion = 3,
        Active = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static RouteImport CreateImport(Guid sourceId, long version, string path) => new()
    {
        Id = Guid.NewGuid(),
        DataSourceId = sourceId,
        Version = version,
        FileName = $"{path}.xlsx",
        FilePath = path,
        Status = RouteImportStatus.Processing,
        CreatedAt = DateTime.UtcNow
    };

    private static byte[] CreateWorkbook(string municipalityName = "CIDADE A")
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("SEGUNDA");
        sheet.Cell(2, 2).Value = "ROTA NOVA";
        sheet.Cell(2, 3).Value = "CIDADES DA ROTA";
        sheet.Cell(3, 3).Value = municipalityName;
        sheet.Cell(3, 4).Value = "1";
        sheet.Cell(3, 5).Value = "12.500,00";
        sheet.Cell(4, 2).Value = "Truck";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed class MemoryImportFileStorage(byte[] content) : IImportFileStorage
    {
        public Task<string> SaveAsync(
            Stream contentStream,
            string fileName,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }
}
