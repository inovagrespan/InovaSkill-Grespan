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
    public async Task SyncInferredAssignmentsAsync_IncludesInactiveCustomerFromCurrentSnapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var routeSource = Source(RouteImportCodes.DataSource, "routes", now);
        var customerSource = Source(CustomerImportCodes.DataSource, "customers", now);
        db.DataSources.AddRange(routeSource, customerSource);
        await db.SaveChangesAsync();
        var routeImport = Import(routeSource.Id, now);
        var customerImport = Import(customerSource.Id, now);
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(), StateCode = "SP", Name = "ECHAPORÃ",
            NormalizedName = "ECHAPORA", CreatedAt = now
        };
        var customer = new Customer
        {
            Id = Guid.NewGuid(), DataSourceId = customerSource.Id, ExternalCode = "0001",
            BranchCode = "01", IsActive = false, CreatedAt = now
        };
        var vehicleType = new VehicleType { Id = Guid.NewGuid(), Name = "TRUCK", CapacityKg = 10_000 };
        db.AddRange(routeImport, customerImport, municipality, customer, vehicleType);
        await db.SaveChangesAsync();
        routeSource.CurrentImportId = routeImport.Id;
        customerSource.CurrentImportId = customerImport.Id;
        var route = new Route
        {
            Id = Guid.NewGuid(), ImportId = routeImport.Id, Name = "ROTA A", Weekday = "MONDAY",
            VehicleTypeId = vehicleType.Id, VehicleCapacityKgSnapshot = 10_000, CreatedAt = now
        };
        db.Routes.Add(route);
        db.RouteEntries.Add(new RouteEntry
        {
            Id = Guid.NewGuid(), RouteId = route.Id, Name = municipality.Name, MunicipalityId = municipality.Id,
            Deliveries = 1, AveragePerDay = 100, CreatedAt = now
        });
        db.CustomerSnapshots.Add(new CustomerSnapshot
        {
            Id = Guid.NewGuid(), ImportId = customerImport.Id, CustomerId = customer.Id,
            MunicipalityId = municipality.Id, DocumentNumber = "41819893804", DocumentType = "CPF",
            LegalName = "CLIENTE", TradeName = "CLIENTE", CustomerType = "Mercado",
            SourceRowNumber = 2, CreatedAt = now
        });
        await db.SaveChangesAsync();

        await new RouteCustomerAssignmentSynchronizer(db).SyncInferredAssignmentsAsync(default);

        var assignment = await db.RouteCustomerAssignments.SingleAsync();
        Assert.Equal(customer.Id, assignment.CustomerId);
        Assert.Equal(RouteCustomerAssignmentSource.InferredByMunicipality, assignment.Source);
    }

    [Fact]
    public async Task SyncInferredAssignmentsAsync_UsesDeliveryCountAndDoesNotRepeatCustomersOnSameWeekday()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var routeSource = Source(RouteImportCodes.DataSource, "routes", now);
        var customerSource = Source(CustomerImportCodes.DataSource, "customers", now);
        db.DataSources.AddRange(routeSource, customerSource);
        await db.SaveChangesAsync();
        var routeImport = Import(routeSource.Id, now);
        var customerImport = Import(customerSource.Id, now);
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(), StateCode = "SP", Name = "MARILIA",
            NormalizedName = "MARILIA", CreatedAt = now
        };
        var vehicleType = new VehicleType { Id = Guid.NewGuid(), Name = "TRUCK", CapacityKg = 10_000 };
        var routeOne = new Route
        {
            Id = Guid.NewGuid(), ImportId = routeImport.Id, Name = "ROTA 1", Weekday = "FRIDAY",
            VehicleTypeId = vehicleType.Id, CreatedAt = now
        };
        var routeTwo = new Route
        {
            Id = Guid.NewGuid(), ImportId = routeImport.Id, Name = "ROTA 2", Weekday = "FRIDAY",
            VehicleTypeId = vehicleType.Id, CreatedAt = now
        };
        db.AddRange(routeImport, customerImport, municipality, vehicleType, routeOne, routeTwo);
        await db.SaveChangesAsync();
        routeSource.CurrentImportId = routeImport.Id;
        customerSource.CurrentImportId = customerImport.Id;
        db.RouteEntries.AddRange(
            new RouteEntry
            {
                Id = Guid.NewGuid(), RouteId = routeOne.Id, Name = municipality.Name,
                MunicipalityId = municipality.Id, Deliveries = 2, AveragePerDay = 100, CreatedAt = now
            },
            new RouteEntry
            {
                Id = Guid.NewGuid(), RouteId = routeTwo.Id, Name = municipality.Name,
                MunicipalityId = municipality.Id, Deliveries = 2, AveragePerDay = 100, CreatedAt = now
            });
        for (var index = 1; index <= 5; index++)
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(), DataSourceId = customerSource.Id, ExternalCode = $"{index:0000}",
                BranchCode = "01", CreatedAt = now
            };
            db.Customers.Add(customer);
            db.CustomerSnapshots.Add(new CustomerSnapshot
            {
                Id = Guid.NewGuid(), ImportId = customerImport.Id, CustomerId = customer.Id,
                MunicipalityId = municipality.Id, DocumentNumber = $"0000000000{index}",
                DocumentType = "CNPJ", LegalName = $"CLIENTE {index}", TradeName = $"CLIENTE {index}",
                CustomerType = "Mercado", SourceRowNumber = index + 1, CreatedAt = now
            });
        }
        await db.SaveChangesAsync();

        await new RouteCustomerAssignmentSynchronizer(db).SyncInferredAssignmentsAsync(default);

        var assignments = await db.RouteCustomerAssignments.AsNoTracking()
            .OrderBy(item => item.RouteId).ThenBy(item => item.CustomerId).ToListAsync();
        Assert.Equal(4, assignments.Count);
        Assert.Equal(2, assignments.Count(item => item.RouteId == routeOne.Id));
        Assert.Equal(2, assignments.Count(item => item.RouteId == routeTwo.Id));
        Assert.Equal(4, assignments.Select(item => item.CustomerId).Distinct().Count());
        Assert.All(assignments, item => Assert.Equal(
            RouteCustomerAssignmentSource.InferredByMunicipality, item.Source));
    }

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

    [Fact]
    public async Task SyncInferredAssignmentsAsync_UsesImportedRowsFirstAndFillsOnlyRemainingDeliverySlots()
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
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(), StateCode = "SP", Name = "MARILIA", NormalizedName = "MARILIA", CreatedAt = now
        };
        var vehicleType = new VehicleType { Id = Guid.NewGuid(), Name = "TRUCK", CapacityKg = 1000 };
        var route = new Route
        {
            Id = Guid.NewGuid(), ImportId = routeImport.Id, Name = "ROTA A", Weekday = "MONDAY",
            VehicleTypeId = vehicleType.Id, CreatedAt = now
        };
        db.AddRange(routeImport, customerImport, mappingImport, municipality, vehicleType, route);
        await db.SaveChangesAsync();
        routeSource.CurrentImportId = routeImport.Id;
        customerSource.CurrentImportId = customerImport.Id;
        mappingSource.CurrentImportId = mappingImport.Id;
        db.RouteEntries.Add(new RouteEntry
        {
            Id = Guid.NewGuid(), RouteId = route.Id, Name = municipality.Name,
            MunicipalityId = municipality.Id, Deliveries = 2, AveragePerDay = 100, CreatedAt = now
        });

        var customers = Enumerable.Range(1, 3).Select(index => new Customer
        {
            Id = Guid.NewGuid(), DataSourceId = customerSource.Id, ExternalCode = $"{index:0000}",
            BranchCode = "01", CreatedAt = now
        }).ToArray();
        db.Customers.AddRange(customers);
        db.CustomerSnapshots.AddRange(customers.Select((customer, index) => new CustomerSnapshot
        {
            Id = Guid.NewGuid(), ImportId = customerImport.Id, CustomerId = customer.Id,
            MunicipalityId = municipality.Id, DocumentNumber = $"0000000000{index + 1}", DocumentType = "CNPJ",
            LegalName = $"CLIENTE {index + 1}", TradeName = $"CLIENTE {index + 1}", CustomerType = "Mercado",
            SourceRowNumber = index + 2, CreatedAt = now
        }));
        db.CustomerRouteMappings.Add(new CustomerRouteMapping
        {
            Id = Guid.NewGuid(), ImportId = mappingImport.Id, CustomerId = customers[1].Id,
            SheetName = "Rotas", SourceRowNumber = 2, Weekday = "MONDAY", RouteName = route.Name,
            NormalizedRouteName = route.Name, MarketName = "CLIENTE 2", MunicipalityName = municipality.Name,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        await new RouteCustomerAssignmentSynchronizer(db).SyncInferredAssignmentsAsync(default);

        var assignments = await db.RouteCustomerAssignments.AsNoTracking().ToListAsync();
        Assert.Equal(2, assignments.Count);
        Assert.Contains(assignments, item => item.CustomerId == customers[1].Id &&
            item.Source == RouteCustomerAssignmentSource.Imported);
        Assert.Contains(assignments, item => item.Source == RouteCustomerAssignmentSource.InferredByMunicipality);
        Assert.Equal(2, assignments.Select(item => item.CustomerId).Distinct().Count());
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
