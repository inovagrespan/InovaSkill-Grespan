using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class CustomersControllerTests
{
    [Fact]
    public async Task List_UsesOnlyCurrentImport_AndAppliesSearchBeforePagination()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot,
            NextImportVersion = 3, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var oldImport = Import(source.Id, 1);
        var currentImport = Import(source.Id, 2);
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(), StateCode = "SP", Name = "BADY BASSITT",
            NormalizedName = "BADY BASSITT", CreatedAt = DateTime.UtcNow
        };
        var customer = new Customer
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, BranchCode = "01",
            ExternalCode = "000224", CreatedAt = DateTime.UtcNow
        };
        db.Add(source);
        await db.SaveChangesAsync();
        db.AddRange(oldImport, currentImport, municipality, customer);
        await db.SaveChangesAsync();
        source.CurrentImportId = currentImport.Id;
        db.CustomerSnapshots.AddRange(
            Snapshot(oldImport.Id, customer.Id, municipality.Id, "ANTIGO"),
            Snapshot(currentImport.Id, customer.Id, municipality.Id, "PENIEL 2"));
        db.CustomerRegistrationAddresses.Add(new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = "07050702000200",
            Source = "BRASIL_API", Status = CustomerRegistrationAddressStatuses.Resolved,
            PostalCode = "17500000", StateCode = "SP", City = "MARILIA",
            Street = "AVENIDA BRASIL", Number = "100", Neighborhood = "CENTRO",
            LastAttemptAt = DateTime.UtcNow, ResolvedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await new CustomersController(db).List(1, 25, "000224", null, null, null);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("PENIEL 2", json);
        Assert.DoesNotContain("ANTIGO", json);
        Assert.Contains("\"total\":1", json);
        Assert.Contains("AVENIDA BRASIL", json);
        Assert.Contains("17500000", json);
        Assert.Contains(CustomerRegistrationAddressStatuses.Resolved, json);
        Assert.DoesNotContain("routeAssignments", json);

        var cityResult = await new CustomersController(db).List(1, 25, "bady bassitt", null, null, null);
        var cityJson = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(cityResult).Value);
        Assert.Contains("PENIEL 2", cityJson);
        Assert.Contains("\"total\":1", cityJson);
    }

    [Fact]
    public async Task ConsumptionSummary_UsesSalePeriodsAndReturnsIssueDateForRecentMovements()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"customer-summary-{Guid.NewGuid()}").Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var customerSource = new DataSource {
            Id = Guid.NewGuid(), Code = CustomerImportCodes.DataSource, ProcessorKey = "customers",
            Name = "Clientes", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot,
            NextImportVersion = 2, Active = true, CreatedAt = now, UpdatedAt = now
        };
        var fiscalSource = new DataSource {
            Id = Guid.NewGuid(), Code = FiscalImportCodes.DataSource, ProcessorKey = "fiscal",
            Name = "Fiscal", Type = "EXCEL", ImportMode = DataSourceImportMode.Upsert,
            NextImportVersion = 2, Active = true, CreatedAt = now, UpdatedAt = now
        };
        var routeSource = new DataSource {
            Id = Guid.NewGuid(), Code = RouteImportCodes.DataSource, ProcessorKey = "routes",
            Name = "Rotas", Type = "EXCEL", ImportMode = DataSourceImportMode.Snapshot,
            NextImportVersion = 2, Active = true, CreatedAt = now, UpdatedAt = now
        };
        db.AddRange(customerSource, fiscalSource, routeSource);
        await db.SaveChangesAsync();
        var customerImport = Import(customerSource.Id, 1);
        var fiscalImport = Import(fiscalSource.Id, 1);
        var routeImport = Import(routeSource.Id, 1);
        var municipality = new Municipality { Id = Guid.NewGuid(), StateCode = "SP", Name = "PIRAJU",
            NormalizedName = "PIRAJU", CreatedAt = now };
        var customer = new Customer { Id = Guid.NewGuid(), DataSourceId = customerSource.Id,
            ExternalCode = "001091", BranchCode = "01", CreatedAt = now };
        db.AddRange(customerImport, fiscalImport, routeImport, municipality, customer);
        await db.SaveChangesAsync();
        customerSource.CurrentImportId = customerImport.Id;
        routeSource.CurrentImportId = routeImport.Id;
        var assignedRoute = new Route { Id = Guid.NewGuid(), ImportId = routeImport.Id,
            Name = "ROTA NORTE", Weekday = "MONDAY", VehicleTypeId = Guid.NewGuid(), CreatedAt = now };
        var availableRoute = new Route { Id = Guid.NewGuid(), ImportId = routeImport.Id,
            Name = "ROTA SUL", Weekday = "TUESDAY", VehicleTypeId = Guid.NewGuid(), CreatedAt = now };
        db.Routes.AddRange(assignedRoute, availableRoute);
        db.RouteCustomerAssignments.Add(new RouteCustomerAssignment {
            Id = Guid.NewGuid(), CustomerId = customer.Id, RouteId = assignedRoute.Id,
            MunicipalityId = municipality.Id, Source = RouteCustomerAssignmentSource.Imported,
            CreatedAt = now, UpdatedAt = now });
        db.CustomerSnapshots.Add(Snapshot(customerImport.Id, customer.Id, municipality.Id, "MERCADO"));
        db.CustomerRegistrationAddresses.Add(new CustomerRegistrationAddress
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, DocumentNumber = "07050702000200",
            Source = "BRASIL_API", Status = CustomerRegistrationAddressStatuses.Resolved,
            PostalCode = "18800000", StateCode = "SP", City = "PIRAJU",
            Street = "RUA DAS FLORES", Number = "42", Complement = "SALA 2", Neighborhood = "CENTRO",
            LastAttemptAt = now, ResolvedAt = now, CreatedAt = now, UpdatedAt = now
        });
        var reference = new DateOnly(2026, 7, 6);
        AddMovement(db, fiscalSource.Id, fiscalImport.Id, customer.Id, reference, "CURRENT", FiscalMovementCategory.Sale, 100, 2, 25);
        AddMovement(db, fiscalSource.Id, fiscalImport.Id, customer.Id, reference.AddDays(-30), "PREVIOUS", FiscalMovementCategory.Sale, 50, 1, 20);
        AddMovement(db, fiscalSource.Id, fiscalImport.Id, customer.Id, reference.AddDays(-70), "OLDER", FiscalMovementCategory.Sale, 150, 3, 10);
        AddMovement(db, fiscalSource.Id, fiscalImport.Id, customer.Id, reference.AddDays(-1), "RETURN", FiscalMovementCategory.Return, 999, 10, 100);
        AddMovement(db, fiscalSource.Id, fiscalImport.Id, customer.Id, reference.AddDays(-2), "BONUS", FiscalMovementCategory.Bonus, 60);
        await db.SaveChangesAsync();

        var result = await new CustomersController(db).ConsumptionSummary(customer.Id, reference);
        var json = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"salesWeightLast30Days\":100", json);
        Assert.Contains("\"salesWeightPrevious30Days\":50", json);
        Assert.Contains("\"variationPercentage\":100", json);
        Assert.Contains("\"averageMonthlySalesWeight90Days\":100", json);
        Assert.Contains("\"averageMonthlySalesWeight12Months\":25", json);
        Assert.Contains("\"saleDocumentsLast30Days\":1", json);
        Assert.Contains("\"averageSalesWeightPerDocument12Months\":100", json);
        Assert.Contains("\"averageMonthlyCalculatedSalesAmount12Months\":8.33", json);
        Assert.Contains("\"returnWeight12Months\":999", json);
        Assert.Contains("\"bonusWeight12Months\":60", json);
        Assert.Contains("\"monthlyTimeline\":[", json);
        Assert.Contains("\"month\":\"2026-07\"", json);
        Assert.Contains("\"salesWeightKg\":100", json);
        Assert.Contains("\"salesDocumentCount\":1", json);
        Assert.Contains("\"calculatedSalesAmount\":50", json);
        Assert.Contains("\"issueDate\":\"2026-07-05\"", json);
        Assert.Contains("\"documentNumber\":\"RETURN\"", json);
        Assert.Contains("\"registrationAddress\":", json);
        Assert.Contains("\"street\":\"RUA DAS FLORES\"", json);
        Assert.Contains("\"number\":\"42\"", json);
        Assert.Contains("\"neighborhood\":\"CENTRO\"", json);
        Assert.Contains("\"complement\":\"SALA 2\"", json);
        Assert.Contains("\"postalCode\":\"18800000\"", json);
        Assert.Contains("\"routeName\":\"ROTA NORTE\"", json);
        Assert.Contains("\"weekday\":\"MONDAY\"", json);
        Assert.DoesNotContain("\"date\":", json);

        var addResult = await new CustomersController(db).AddRouteAssignment(
            customer.Id, new AddCustomerRouteAssignmentRequest(availableRoute.Id));
        Assert.IsType<CreatedResult>(addResult);
        var manual = await db.RouteCustomerAssignments.SingleAsync(assignment =>
            assignment.CustomerId == customer.Id && assignment.RouteId == availableRoute.Id);
        Assert.Equal(RouteCustomerAssignmentSource.Manual, manual.Source);
        Assert.Equal(municipality.Id, manual.MunicipalityId);

        var duplicateResult = await new CustomersController(db).AddRouteAssignment(
            customer.Id, new AddCustomerRouteAssignmentRequest(availableRoute.Id));
        Assert.IsType<ConflictObjectResult>(duplicateResult);

        var projectionResult = await new CustomersController(db).Projection(customer.Id);
        var projectionJson = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(projectionResult).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"baseEndMonth\":\"2026-06-01\"", projectionJson);
        Assert.Contains("\"sourceCoverageDate\":\"2026-07-06\"", projectionJson);
        Assert.Contains("\"historicalMonthlyAverage\":16.667", projectionJson);
        Assert.Contains("\"historicalMonthlyAverage\":4.17", projectionJson);
        Assert.Contains("\"activeMonths\":2", projectionJson);
        Assert.Contains("\"quality\":\"INSUFFICIENT\"", projectionJson);
        Assert.Contains("\"partialSourceMonthExcluded\":true", projectionJson);
    }

    private static RouteImport Import(Guid sourceId, long version) => new()
    {
        Id = Guid.NewGuid(), DataSourceId = sourceId, Version = version,
        FileName = $"{version}.xlsx", FilePath = $"{version}", Status = RouteImportStatus.Completed,
        CreatedAt = DateTime.UtcNow
    };

    private static CustomerSnapshot Snapshot(Guid importId, Guid customerId, Guid municipalityId, string tradeName) => new()
    {
        Id = Guid.NewGuid(), ImportId = importId, CustomerId = customerId, MunicipalityId = municipalityId,
        DocumentNumber = "07050702000200", DocumentType = "CNPJ", LegalName = "PENIEL",
        TradeName = tradeName, CustomerType = "Solidario", SourceRowNumber = 2, CreatedAt = DateTime.UtcNow
    };

    private static void AddMovement(ImportDbContext db, Guid sourceId, Guid importId, Guid customerId,
        DateOnly date, string number, FiscalMovementCategory category, decimal weight,
        decimal quantity = 1, decimal? unitValue = null)
    {
        var document = new FiscalDocument {
            Id = Guid.NewGuid(), DataSourceId = sourceId, DocumentNumber = number, Series = "1",
            DocumentType = "NF", MovementType = "NF", IssueDate = date, CustomerId = customerId,
            CustomerCodeAtIssue = "001091", BranchCodeAtIssue = "01", CustomerNameAtIssue = "MERCADO",
            CityNameAtIssue = "PIRAJU", StateCodeAtIssue = "SP", OperationCode = category.ToString(),
            OperationDescription = category.ToString(), MovementCategory = category,
            FirstSeenImportId = importId, LastSeenImportId = importId, CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        document.Items.Add(new FiscalDocumentItem {
            Id = Guid.NewGuid(), ItemNumber = "1", ProductCode = "P1", ProductDescription = "Produto",
            ProductGroupCode = "", ProductGroupDescription = "", Quantity = quantity, UnitValue = unitValue,
            GrossWeightKg = weight, SourceTotalValue = -999_999,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.FiscalDocuments.Add(document);
    }
}
