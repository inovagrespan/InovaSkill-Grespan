using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class LogisticsMapControllerTests
{
    [Fact]
    public async Task Customers_ReturnsCurrentCustomersWithResolvedCoordinatesAndPendingCount()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"logistics-map-{Guid.NewGuid()}").Options);
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
        source.CurrentImportId = import.Id;
        var marilia = Municipality("Marília", "MARILIA");
        var pending = Municipality("Sem Coordenada", "SEM COORDENADA");
        var firstCustomer = Customer(source.Id, "0001", "01");
        var secondCustomer = Customer(source.Id, "0002", "01");
        db.AddRange(source, import, marilia, pending, firstCustomer, secondCustomer);
        await db.SaveChangesAsync();
        db.MunicipalityCoordinates.Add(new MunicipalityCoordinate {
            Id = Guid.NewGuid(), MunicipalityId = marilia.Id, Latitude = -22.2171m,
            Longitude = -49.9501m, Source = "test", Status = MunicipalityCoordinateStatuses.Resolved,
            CreatedAt = now, UpdatedAt = now, ResolvedAt = now
        });
        db.CustomerSnapshots.AddRange(
            Snapshot(import.Id, firstCustomer.Id, marilia.Id, "Padaria Real"),
            Snapshot(import.Id, secondCustomer.Id, pending.Id, "Mercado Pendente"));
        await db.SaveChangesAsync();

        var result = await new LogisticsMapController(db).Customers(CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"total\":2", json);
        Assert.Contains("\"visible\":1", json);
        Assert.Contains("\"withoutCoordinates\":1", json);
        Assert.Contains("Padaria Real", json);
        Assert.Contains("\"locationPrecision\":\"Municipality\"", json);
        Assert.DoesNotContain("Mercado Pendente", json);
    }

    private static Municipality Municipality(string name, string normalizedName) => new()
    {
        Id = Guid.NewGuid(),
        StateCode = "SP",
        Name = name,
        NormalizedName = normalizedName,
        CreatedAt = DateTime.UtcNow
    };

    private static Customer Customer(Guid sourceId, string code, string branch) => new()
    {
        Id = Guid.NewGuid(),
        DataSourceId = sourceId,
        BranchCode = branch,
        ExternalCode = code,
        CreatedAt = DateTime.UtcNow
    };

    private static CustomerSnapshot Snapshot(
        Guid importId,
        Guid customerId,
        Guid municipalityId,
        string tradeName) => new()
    {
        Id = Guid.NewGuid(),
        ImportId = importId,
        CustomerId = customerId,
        MunicipalityId = municipalityId,
        DocumentNumber = "",
        DocumentType = "UNKNOWN",
        LegalName = tradeName,
        TradeName = tradeName,
        CustomerType = "Mercado",
        SourceRowNumber = 1,
        CreatedAt = DateTime.UtcNow
    };
}
