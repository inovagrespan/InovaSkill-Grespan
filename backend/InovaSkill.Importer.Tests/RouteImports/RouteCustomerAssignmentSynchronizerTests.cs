using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class RouteCustomerAssignmentSynchronizerTests
{
    [Fact]
    public async Task SyncInferredAssignmentsAsync_PreservesManualAssignmentWhenImportedMappingMatches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var routeSource = Source(RouteImportCodes.DataSource, "routes", now);
        var customerSource = Source(CustomerImportCodes.DataSource, "customers", now);
        var mappingSource = Source(CustomerRouteAssignmentImportCodes.DataSource, "customer-route-assignments", now);
        db.DataSources.AddRange(routeSource, customerSource, mappingSource);
        await db.SaveChangesAsync();
        var routeImport = Import(routeSource.Id, now);
        var customerImport = Import(customerSource.Id, now);
        var mappingImport = Import(mappingSource.Id, now);
        var municipality = new Municipality { Id = Guid.NewGuid(), StateCode = "SP", Name = "MARILIA",
            NormalizedName = "MARILIA", CreatedAt = now };
        var customer = new Customer { Id = Guid.NewGuid(), DataSourceId = customerSource.Id,
            ExternalCode = "0001", BranchCode = "01", CreatedAt = now };
        var vehicleType = new VehicleType { Id = Guid.NewGuid(), Name = "TRUCK", CapacityKg = 1000 };
        db.AddRange(routeImport, customerImport, mappingImport, municipality, customer, vehicleType);
        await db.SaveChangesAsync();
        routeSource.CurrentImportId = routeImport.Id;
        customerSource.CurrentImportId = customerImport.Id;
        mappingSource.CurrentImportId = mappingImport.Id;
        var route = new Route { Id = Guid.NewGuid(), ImportId = routeImport.Id, Name = "ROTA A",
            Weekday = "MONDAY", VehicleTypeId = vehicleType.Id, CreatedAt = now };
        db.Routes.Add(route);
        db.CustomerSnapshots.Add(new CustomerSnapshot { Id = Guid.NewGuid(), ImportId = customerImport.Id,
            CustomerId = customer.Id, MunicipalityId = municipality.Id, DocumentNumber = "07050702000200",
            DocumentType = "CNPJ", LegalName = "CLIENTE", TradeName = "CLIENTE", CustomerType = "Mercado",
            SourceRowNumber = 2, CreatedAt = now });
        db.CustomerRouteMappings.Add(new CustomerRouteMapping { Id = Guid.NewGuid(), ImportId = mappingImport.Id,
            CustomerId = customer.Id, SheetName = "Rotas", SourceRowNumber = 2, Weekday = "MONDAY",
            RouteName = route.Name, NormalizedRouteName = route.Name, MarketName = "CLIENTE",
            MunicipalityName = municipality.Name, CreatedAt = now });
        db.RouteCustomerAssignments.Add(new RouteCustomerAssignment { Id = Guid.NewGuid(), RouteId = route.Id,
            CustomerId = customer.Id, MunicipalityId = municipality.Id, Source = RouteCustomerAssignmentSource.Manual,
            CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        await new RouteCustomerAssignmentSynchronizer(db).SyncInferredAssignmentsAsync(default);

        var assignment = await db.RouteCustomerAssignments.SingleAsync();
        Assert.Equal(RouteCustomerAssignmentSource.Manual, assignment.Source);
    }

    private static DataSource Source(string code, string processor, DateTime now) => new()
    {
        Id = Guid.NewGuid(), Code = code, ProcessorKey = processor, Name = code, Type = "EXCEL",
        ImportMode = DataSourceImportMode.Snapshot, NextImportVersion = 2, Active = true,
        CreatedAt = now, UpdatedAt = now
    };

    private static RouteImport Import(Guid sourceId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), DataSourceId = sourceId, Version = 1, FileName = "source.xlsx",
        FilePath = "source.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now, FinishedAt = now
    };
}
