using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class FiscalDocumentsControllerTests
{
    [Fact]
    public async Task ReturnRate_ReturnsZeroWhenThereAreNoFiscalDocuments()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"fiscal-return-rate-empty-{Guid.NewGuid()}").Options);

        var result = await new FiscalDocumentsController(db).ReturnRate(cancellationToken: default);
        var json = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"periodDays\":30", json);
        Assert.Contains("\"dateFrom\":null", json);
        Assert.Contains("\"dateTo\":null", json);
        Assert.Contains("\"salesWeightKg\":0", json);
        Assert.Contains("\"returnWeightKg\":0", json);
        Assert.Contains("\"returnRatePercent\":0", json);
    }

    [Fact]
    public async Task ReturnRate_CalculatesReturnWeightOverSalesWeightForSelectedPeriod()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"fiscal-return-rate-{Guid.NewGuid()}").Options);
        var now = DateTime.UtcNow;
        var source = Source(now);
        var import = Import(source.Id, now);
        db.AddRange(source, import);
        AddDocument(db, source.Id, import.Id, "SALE-1", new DateOnly(2026, 7, 10), FiscalMovementCategory.Sale, 100);
        AddDocument(db, source.Id, import.Id, "SALE-2", new DateOnly(2026, 7, 8), FiscalMovementCategory.Sale, 50);
        AddDocument(db, source.Id, import.Id, "RETURN-1", new DateOnly(2026, 7, 9), FiscalMovementCategory.Return, 15);
        AddDocument(db, source.Id, import.Id, "OLD-RETURN", new DateOnly(2026, 6, 1), FiscalMovementCategory.Return, 999);
        AddDocument(db, source.Id, import.Id, "BONUS", new DateOnly(2026, 7, 9), FiscalMovementCategory.Bonus, 999);
        await db.SaveChangesAsync();

        var result = await new FiscalDocumentsController(db).ReturnRate(7, new DateOnly(2026, 7, 10), default);
        var json = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"periodDays\":7", json);
        Assert.Contains("\"dateFrom\":\"2026-07-04\"", json);
        Assert.Contains("\"dateTo\":\"2026-07-10\"", json);
        Assert.Contains("\"salesWeightKg\":150", json);
        Assert.Contains("\"returnWeightKg\":15", json);
        Assert.Contains("\"returnRatePercent\":10", json);
        Assert.DoesNotContain("999", json);
    }

    [Fact]
    public async Task ReturnRate_UsesLatestFiscalDateAndReturnsZeroWhenSalesBaseIsZero()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"fiscal-return-rate-zero-{Guid.NewGuid()}").Options);
        var now = DateTime.UtcNow;
        var source = Source(now);
        var import = Import(source.Id, now);
        db.AddRange(source, import);
        AddDocument(db, source.Id, import.Id, "RETURN-ONLY", new DateOnly(2026, 7, 10), FiscalMovementCategory.Return, 25);
        await db.SaveChangesAsync();

        var result = await new FiscalDocumentsController(db).ReturnRate(cancellationToken: default);
        var json = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"dateTo\":\"2026-07-10\"", json);
        Assert.Contains("\"salesWeightKg\":0", json);
        Assert.Contains("\"returnWeightKg\":25", json);
        Assert.Contains("\"returnRatePercent\":0", json);
    }

    [Fact]
    public async Task Get_CalculatesObservableTotalsFromItemsWithoutUsingSourceTotal()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"fiscal-details-{Guid.NewGuid()}").Options);
        var now = DateTime.UtcNow;
        var source = Source(now);
        var import = Import(source.Id, now);
        var document = new FiscalDocument {
            Id = Guid.NewGuid(), DataSourceId = source.Id, DocumentNumber = "424808", Series = "1",
            DocumentType = "NF", MovementType = "NF", IssueDate = new DateOnly(2026, 7, 1),
            CustomerCodeAtIssue = "001091", BranchCodeAtIssue = "01", CustomerNameAtIssue = "Mercado",
            CityNameAtIssue = "Piraju", StateCodeAtIssue = "SP", OperationCode = "01",
            OperationDescription = "VENDA", MovementCategory = FiscalMovementCategory.Sale,
            FirstSeenImportId = import.Id, LastSeenImportId = import.Id, CreatedAt = now, UpdatedAt = now
        };
        document.Items.Add(Item("1", 2, 10, 5, -500));
        document.Items.Add(Item("2", 3, null, 3, 999_999));
        db.AddRange(source, import, document);
        await db.SaveChangesAsync();

        var result = await new FiscalDocumentsController(db).Get(document.Id, default);
        var json = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"itemCount\":2", json);
        Assert.Contains("\"grossWeightKg\":8", json);
        Assert.Contains("\"totalQuantity\":5", json);
        Assert.Contains("\"calculatedTotalAmount\":20", json);
        Assert.Contains("\"calculatedAmount\":20", json);
        Assert.DoesNotContain("999999", json);
        Assert.DoesNotContain("-500", json);
    }

    [Fact]
    public async Task Get_ComparesInvoiceTicketAgainstCustomerHistoricalAverageTicket()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"fiscal-commercial-quality-{Guid.NewGuid()}").Options);
        var now = DateTime.UtcNow;
        var customerId = Guid.NewGuid();
        var source = Source(now);
        var import = Import(source.Id, now);
        var firstHistoricalSale = Document(source.Id, import.Id, customerId, "100001", new DateOnly(2026, 6, 1), FiscalMovementCategory.Sale, now);
        var secondHistoricalSale = Document(source.Id, import.Id, customerId, "100002", new DateOnly(2026, 6, 15), FiscalMovementCategory.Sale, now);
        var currentSale = Document(source.Id, import.Id, customerId, "100003", new DateOnly(2026, 7, 1), FiscalMovementCategory.Sale, now);
        var otherCustomerSale = Document(source.Id, import.Id, Guid.NewGuid(), "200001", new DateOnly(2026, 6, 1), FiscalMovementCategory.Sale, now);
        var customerReturn = Document(source.Id, import.Id, customerId, "100004", new DateOnly(2026, 6, 20), FiscalMovementCategory.Return, now);
        firstHistoricalSale.Items.Add(Item("1", 1, 100, 1, 100));
        secondHistoricalSale.Items.Add(Item("1", 1, 200, 1, 200));
        currentSale.Items.Add(Item("1", 1, 300, 1, 300));
        otherCustomerSale.Items.Add(Item("1", 1, 999, 1, 999));
        customerReturn.Items.Add(Item("1", 1, 700, 1, 700));
        db.AddRange(source, import, firstHistoricalSale, secondHistoricalSale, currentSale, otherCustomerSale, customerReturn);
        await db.SaveChangesAsync();

        var result = await new FiscalDocumentsController(db).Get(currentSale.Id, default);
        var json = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"calculatedTotalAmount\":300", json);
        Assert.Contains("\"customerAverageTicket\":150", json);
        Assert.Contains("\"historicalSaleDocumentCount\":2", json);
        Assert.Contains("\"ticketVariationPercentage\":100", json);
        Assert.Contains($"\"classification\":\"{CommercialSaleQualityCalculator.GreatSaleClassification}\"", json);
        Assert.Contains("Nota acima do ticket", json);
        Assert.DoesNotContain("999", json);
        Assert.DoesNotContain("700", json);
    }

    [Theory]
    [InlineData(114, CommercialSaleQualityCalculator.RegularSaleClassification)]
    [InlineData(115, CommercialSaleQualityCalculator.GreatSaleClassification)]
    [InlineData(85, CommercialSaleQualityCalculator.AttentionSaleClassification)]
    public void CommercialSaleQualityCalculator_ClassifiesTicketVariationByNamedThresholds(
        decimal invoiceTotalAmount,
        string expectedClassification)
    {
        var result = CommercialSaleQualityCalculator.Calculate(new CommercialSaleQualityInput(
            invoiceTotalAmount,
            100,
            CommercialSaleQualityCalculator.MinimumHistoricalSaleDocuments,
            true));

        Assert.Equal(expectedClassification, result.Classification);
    }

    [Fact]
    public void CommercialSaleQualityCalculator_RequiresEnoughHistoricalSales()
    {
        var result = CommercialSaleQualityCalculator.Calculate(new CommercialSaleQualityInput(
            200,
            100,
            CommercialSaleQualityCalculator.MinimumHistoricalSaleDocuments - 1,
            true));

        Assert.Null(result.TicketVariationPercentage);
        Assert.Equal(CommercialSaleQualityCalculator.InsufficientHistoryClassification, result.Classification);
    }

    private static FiscalDocumentItem Item(
        string number, decimal quantity, decimal? unitValue, decimal weight, decimal sourceTotal) => new() {
        Id = Guid.NewGuid(), ItemNumber = number, ProductCode = $"P{number}",
        ProductDescription = $"Produto {number}", ProductGroupCode = "G1",
        ProductGroupDescription = "Grupo", Quantity = quantity, UnitValue = unitValue,
        GrossWeightKg = weight, SourceTotalValue = sourceTotal,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static DataSource Source(DateTime now) => new() {
        Id = Guid.NewGuid(), Code = "FISCAL", ProcessorKey = "fiscal", Name = "Fiscal",
        Type = "EXCEL", ImportMode = DataSourceImportMode.Upsert, NextImportVersion = 2,
        Active = true, CreatedAt = now, UpdatedAt = now
    };

    private static RouteImport Import(Guid sourceId, DateTime now) => new() {
        Id = Guid.NewGuid(), DataSourceId = sourceId, Version = 1, FileName = "fiscal.xlsx",
        FilePath = "fiscal.xlsx", Status = RouteImportStatus.Completed, CreatedAt = now
    };

<<<<<<< HEAD
    private static FiscalDocument Document(
        Guid sourceId,
        Guid importId,
        Guid customerId,
        string documentNumber,
        DateOnly issueDate,
        FiscalMovementCategory category,
        DateTime now) => new() {
        Id = Guid.NewGuid(), DataSourceId = sourceId, DocumentNumber = documentNumber, Series = "1",
        DocumentType = "NF", MovementType = "NF", IssueDate = issueDate, CustomerId = customerId,
        CustomerCodeAtIssue = "001091", BranchCodeAtIssue = "01", CustomerNameAtIssue = "Mercado",
        CityNameAtIssue = "Piraju", StateCodeAtIssue = "SP", OperationCode = "01",
        OperationDescription = category == FiscalMovementCategory.Sale ? "VENDA" : "DEVOLUÇÃO",
        MovementCategory = category, FirstSeenImportId = importId, LastSeenImportId = importId,
        CreatedAt = now, UpdatedAt = now
    };
=======
    private static void AddDocument(
        ImportDbContext db,
        Guid sourceId,
        Guid importId,
        string documentNumber,
        DateOnly issueDate,
        FiscalMovementCategory category,
        decimal grossWeightKg)
    {
        var document = new FiscalDocument {
            Id = Guid.NewGuid(), DataSourceId = sourceId, DocumentNumber = documentNumber, Series = "1",
            DocumentType = "NF", MovementType = "NF", IssueDate = issueDate,
            CustomerCodeAtIssue = "001091", BranchCodeAtIssue = "01", CustomerNameAtIssue = "Mercado",
            CityNameAtIssue = "Piraju", StateCodeAtIssue = "SP", OperationCode = category.ToString(),
            OperationDescription = category.ToString(), MovementCategory = category,
            FirstSeenImportId = importId, LastSeenImportId = importId, CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        document.Items.Add(Item("1", 1, null, grossWeightKg, grossWeightKg));
        db.FiscalDocuments.Add(document);
    }
>>>>>>> c21ebb21d9b03f71e75e78c3d0c9db9b97a658d3
}
