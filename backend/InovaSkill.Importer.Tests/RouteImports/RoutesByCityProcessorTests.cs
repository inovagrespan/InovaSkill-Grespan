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
        Assert.Equal(1m, currentRoute.WeightOccupancy);
        Assert.Equal(1m, currentRoute.OverallOccupancy);
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

    private static byte[] CreateWorkbook()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("SEGUNDA");
        sheet.Cell(2, 2).Value = "ROTA NOVA";
        sheet.Cell(2, 3).Value = "CIDADES DA ROTA";
        sheet.Cell(3, 3).Value = "CIDADE A";
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
