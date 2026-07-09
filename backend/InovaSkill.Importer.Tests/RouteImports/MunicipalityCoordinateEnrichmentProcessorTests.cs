using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class MunicipalityCoordinateEnrichmentProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ResolvesOnlyMunicipalitiesUsedByCurrentCustomerSnapshot()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"municipality-coordinates-{Guid.NewGuid()}").Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var source = new DataSource {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot,
            NextImportVersion = 2, Active = true, CreatedAt = now, UpdatedAt = now
        };
        var import = new RouteImport {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 1, FileName = "clientes.xlsx",
            FilePath = "clientes.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
        };
        var marilia = Municipality("MARÍLIA", "MARILIA");
        var unknown = Municipality("Cidade Sem Base", "CIDADE SEM BASE");
        var unused = Municipality("Bauru", "BAURU");
        var firstCustomer = Customer(source.Id, "0001");
        var secondCustomer = Customer(source.Id, "0002");
        db.AddRange(source, import, marilia, unknown, unused, firstCustomer, secondCustomer);
        await db.SaveChangesAsync();
        db.CustomerSnapshots.AddRange(
            Snapshot(import.Id, firstCustomer.Id, marilia.Id),
            Snapshot(import.Id, secondCustomer.Id, unknown.Id));
        await db.SaveChangesAsync();

        var processor = new MunicipalityCoordinateEnrichmentProcessor(
            db,
            new EmbeddedMunicipalityCoordinateProvider());
        await processor.ProcessAsync(import.Id, CancellationToken.None);

        var mariliaCoordinate = await db.MunicipalityCoordinates.SingleAsync(item =>
            item.MunicipalityId == marilia.Id);
        Assert.Equal(MunicipalityCoordinateStatuses.Resolved, mariliaCoordinate.Status);
        Assert.Equal(-22.2171m, mariliaCoordinate.Latitude);
        Assert.Equal(-49.9501m, mariliaCoordinate.Longitude);
        Assert.Equal("3529005", (await db.Municipalities.FindAsync(marilia.Id))!.IbgeCode);

        var failedCoordinate = await db.MunicipalityCoordinates.SingleAsync(item =>
            item.MunicipalityId == unknown.Id);
        Assert.Equal(MunicipalityCoordinateStatuses.Failed, failedCoordinate.Status);
        Assert.Null(failedCoordinate.Latitude);
        Assert.Null(failedCoordinate.Longitude);

        Assert.False(await db.MunicipalityCoordinates.AnyAsync(item => item.MunicipalityId == unused.Id));
    }

    private static Municipality Municipality(string name, string normalizedName) => new()
    {
        Id = Guid.NewGuid(),
        StateCode = "SP",
        Name = name,
        NormalizedName = normalizedName,
        CreatedAt = DateTime.UtcNow
    };

    private static Customer Customer(Guid sourceId, string code) => new()
    {
        Id = Guid.NewGuid(),
        DataSourceId = sourceId,
        BranchCode = "01",
        ExternalCode = code,
        CreatedAt = DateTime.UtcNow
    };

    private static CustomerSnapshot Snapshot(Guid importId, Guid customerId, Guid municipalityId) => new()
    {
        Id = Guid.NewGuid(),
        ImportId = importId,
        CustomerId = customerId,
        MunicipalityId = municipalityId,
        DocumentNumber = "",
        DocumentType = "UNKNOWN",
        LegalName = "Cliente",
        TradeName = "Cliente",
        CustomerType = "Mercado",
        SourceRowNumber = 1,
        CreatedAt = DateTime.UtcNow
    };
}
