using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class CommercialTransactionsControllerTests
{
    [Fact]
    public async Task GetPaged_FiltersByProductGroupAndTransactionType()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new CommercialTransactionsController(db);

        var result = await controller.GetPaged(productGroup: "Grupo A", transactionType: "Venda");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<CommercialTransactionDto>>(ok.Value);
        Assert.Equal(2, payload.Total);
        Assert.Collection(
            payload.Items,
            first =>
            {
                Assert.Equal("P3", first.ProductCode);
                Assert.Equal("Grupo A", first.ProductGroup);
                Assert.Equal("Venda", first.TransactionType);
            },
            second =>
            {
                Assert.Equal("P1", second.ProductCode);
                Assert.Equal("Grupo A", second.ProductGroup);
                Assert.Equal("Venda", second.TransactionType);
            });
    }

    [Fact]
    public async Task GetSummary_AppliesProductGroupAndTransactionTypeFilters()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new CommercialTransactionsController(db);

        var result = await controller.GetSummary(productGroup: "Grupo A", transactionType: "Venda");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CommercialTransactionSummaryResponseDto>(ok.Value);
        Assert.Equal(2, payload.TotalRecords);
        Assert.Equal(400m, payload.TotalAmount);
        Assert.Equal(40m, payload.TotalQuantity);
        Assert.Equal(17m, payload.TotalWeightKg);
        Assert.Equal(2, payload.Items.Count);
        Assert.All(payload.Items, item => Assert.Equal(1, item.DocumentCount));
        Assert.Contains(payload.Items, item => item.SingleDocumentNumber == "NF-1");
    }

    [Fact]
    public async Task GetPaged_AcceptsUnspecifiedDateInputsFromHtmlDateFields()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new CommercialTransactionsController(db);

        var result = await controller.GetPaged(
            dateFrom: new DateTime(2026, 5, 1),
            dateTo: new DateTime(2026, 5, 1));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<CommercialTransactionDto>>(ok.Value);
        var item = Assert.Single(payload.Items);
        Assert.Equal("NF-1", item.DocumentNumber);
    }

    [Fact]
    public async Task GetTimeline_GroupsMonthlyWhenRequested()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new CommercialTransactionsController(db);

        var result = await controller.GetTimeline(granularity: "monthly");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CommercialTransactionTimelineResponseDto>(ok.Value);

        Assert.Equal("month", payload.Granularity);
        Assert.Collection(
            payload.Items,
            may =>
            {
                Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), may.PeriodStart);
                Assert.Equal(300m, may.TotalAmount);
                Assert.Equal(30m, may.TotalQuantity);
                Assert.Equal(13m, may.TotalWeightKg);
                Assert.Equal(2, may.RecordCount);
            },
            june =>
            {
                Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), june.PeriodStart);
                Assert.Equal(700m, june.TotalAmount);
                Assert.Equal(70m, june.TotalQuantity);
                Assert.Equal(29m, june.TotalWeightKg);
                Assert.Equal(2, june.RecordCount);
            });
    }

    [Fact]
    public async Task GetTimeline_GroupsByWeekStartAndRespectsDateFilters()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new CommercialTransactionsController(db);

        var result = await controller.GetTimeline(
            granularity: "weekly",
            dateFrom: new DateTime(2026, 6, 1),
            dateTo: new DateTime(2026, 6, 9));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CommercialTransactionTimelineResponseDto>(ok.Value);

        Assert.Equal("week", payload.Granularity);
        Assert.Collection(
            payload.Items,
            weekOne =>
            {
                Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), weekOne.PeriodStart);
                Assert.Equal(300m, weekOne.TotalAmount);
                Assert.Equal(30m, weekOne.TotalQuantity);
                Assert.Equal(12m, weekOne.TotalWeightKg);
                Assert.Equal(1, weekOne.RecordCount);
            },
            weekTwo =>
            {
                Assert.Equal(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), weekTwo.PeriodStart);
                Assert.Equal(400m, weekTwo.TotalAmount);
                Assert.Equal(40m, weekTwo.TotalQuantity);
                Assert.Equal(17m, weekTwo.TotalWeightKg);
                Assert.Equal(1, weekTwo.RecordCount);
            });
    }

    [Fact]
    public async Task GetTimeline_GroupsDailyWithOnePointPerSaleDate()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new CommercialTransactionsController(db);

        var result = await controller.GetTimeline(granularity: "daily", productGroup: "Grupo A");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CommercialTransactionTimelineResponseDto>(ok.Value);

        Assert.Equal("day", payload.Granularity);
        Assert.Collection(
            payload.Items,
            first =>
            {
                Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), first.PeriodStart);
                Assert.Equal(100m, first.TotalAmount);
                Assert.Equal(10m, first.TotalQuantity);
            },
            second =>
            {
                Assert.Equal(new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc), second.PeriodStart);
                Assert.Equal(300m, second.TotalAmount);
                Assert.Equal(30m, second.TotalQuantity);
            });
    }

    [Fact]
    public async Task GetPaged_CalculatesTotalAmountFromQuantityAndUnitPrice()
    {
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.Add(new Domain.Entities.CommercialTransaction
        {
            DocumentNumber = "NF-CALC",
            TransactionDate = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            CustomerCode = "C1",
            CustomerName = "Empresa A",
            ProductCode = "P1",
            ProductDescription = "Produto 1",
            Quantity = -3m,
            UnitPrice = 12.5m,
            TotalAmount = 9999m,
            TransactionType = "Devolução",
            City = "Campinas",
            ProductGroup = "Grupo A",
            GrossWeightKg = -6m,
            SourceFileJobId = 1
        });
        await db.SaveChangesAsync();
        var controller = new CommercialTransactionsController(db);

        var result = await controller.GetPaged(documentNumber: "NF-CALC");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<CommercialTransactionDto>>(ok.Value);
        var item = Assert.Single(payload.Items);
        Assert.Equal(-37.5m, item.TotalAmount);
    }

    [Fact]
    public async Task GetTimeline_CalculatesTotalAmountFromQuantityAndUnitPrice()
    {
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.Add(new Domain.Entities.CommercialTransaction
        {
            DocumentNumber = "NF-CALC",
            TransactionDate = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            CustomerCode = "C1",
            CustomerName = "Empresa A",
            ProductCode = "P1",
            ProductDescription = "Produto 1",
            Quantity = -3m,
            UnitPrice = 12.5m,
            TotalAmount = 9999m,
            TransactionType = "Devolução",
            City = "Campinas",
            ProductGroup = "Grupo A",
            GrossWeightKg = -6m,
            SourceFileJobId = 1
        });
        await db.SaveChangesAsync();
        var controller = new CommercialTransactionsController(db);

        var result = await controller.GetTimeline(granularity: "daily", documentNumber: "NF-CALC");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CommercialTransactionTimelineResponseDto>(ok.Value);
        var point = Assert.Single(payload.Items);
        Assert.Equal(-37.5m, point.TotalAmount);
        Assert.Equal(-3m, point.TotalQuantity);
        Assert.Equal(-6m, point.TotalWeightKg);
    }

    [Fact]
    public async Task GetPaged_SearchesDocumentAndProductPartiallyIgnoringCase()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new CommercialTransactionsController(db);

        var documentResult = await controller.GetPaged(documentNumber: "nf-3");
        var documentOk = Assert.IsType<OkObjectResult>(documentResult.Result);
        var documentPayload = Assert.IsType<PagedResult<CommercialTransactionDto>>(documentOk.Value);
        var documentItem = Assert.Single(documentPayload.Items);
        Assert.Equal("NF-3", documentItem.DocumentNumber);

        var productResult = await controller.GetPaged(productCode: "produto 4");
        var productOk = Assert.IsType<OkObjectResult>(productResult.Result);
        var productPayload = Assert.IsType<PagedResult<CommercialTransactionDto>>(productOk.Value);
        var productItem = Assert.Single(productPayload.Items);
        Assert.Equal("P4", productItem.ProductCode);
        Assert.Equal("Produto 4", productItem.ProductDescription);
    }

    [Fact]
    public async Task GetSummary_ReturnsDocumentCountAndSingleDocumentForRankingTraceability()
    {
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.AddRange(
            BuildTransaction("NF-10", "Empresa A", "P1", "Produto 1", new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc), 2m, 10m),
            BuildTransaction("NF-11", "Empresa A", "P2", "Produto 2", new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc), 3m, 10m),
            BuildTransaction("NF-20", "Empresa B", "P3", "Produto 3", new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc), 4m, 10m));
        await db.SaveChangesAsync();
        var controller = new CommercialTransactionsController(db);

        var result = await controller.GetSummary(sortBy: "amount", referenceDate: new DateTime(2026, 6, 9));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CommercialTransactionSummaryResponseDto>(ok.Value);
        Assert.Collection(
            payload.Items,
            first =>
            {
                Assert.Equal("Empresa A", first.CompanyName);
                Assert.Equal(2, first.DocumentCount);
                Assert.Null(first.SingleDocumentNumber);
            },
            second =>
            {
                Assert.Equal("Empresa B", second.CompanyName);
                Assert.Equal(1, second.DocumentCount);
                Assert.Equal("NF-20", second.SingleDocumentNumber);
            });
    }

    [Fact]
    public async Task GetTimeline_AcceptsGroupByHourAndQuarter()
    {
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.AddRange(
            BuildTransaction("NF-H1", "Empresa A", "P1", "Produto 1", new DateTime(2026, 1, 5, 8, 15, 0, DateTimeKind.Utc), 2m, 10m),
            BuildTransaction("NF-H2", "Empresa A", "P1", "Produto 1", new DateTime(2026, 1, 5, 8, 45, 0, DateTimeKind.Utc), 3m, 10m),
            BuildTransaction("NF-Q2", "Empresa B", "P2", "Produto 2", new DateTime(2026, 4, 10, 9, 0, 0, DateTimeKind.Utc), 4m, 10m));
        await db.SaveChangesAsync();
        var controller = new CommercialTransactionsController(db);

        var hourResult = await controller.GetTimeline(groupBy: "hour", dateFrom: new DateTime(2026, 1, 5), dateTo: new DateTime(2026, 1, 5));
        var hourOk = Assert.IsType<OkObjectResult>(hourResult.Result);
        var hourPayload = Assert.IsType<CommercialTransactionTimelineResponseDto>(hourOk.Value);
        var hourPoint = Assert.Single(hourPayload.Items);
        Assert.Equal("hour", hourPayload.Granularity);
        Assert.Equal(new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc), hourPoint.PeriodStart);
        Assert.Equal(50m, hourPoint.TotalAmount);
        Assert.Equal(2, hourPoint.RecordCount);

        var quarterResult = await controller.GetTimeline(groupBy: "quarter");
        var quarterOk = Assert.IsType<OkObjectResult>(quarterResult.Result);
        var quarterPayload = Assert.IsType<CommercialTransactionTimelineResponseDto>(quarterOk.Value);
        Assert.Equal("quarter", quarterPayload.Granularity);
        Assert.Collection(
            quarterPayload.Items,
            first =>
            {
                Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), first.PeriodStart);
                Assert.Equal(50m, first.TotalAmount);
            },
            second =>
            {
                Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), second.PeriodStart);
                Assert.Equal(40m, second.TotalAmount);
            });
    }

    private static async Task<ImportDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ImportDbContext>().UseSqlite(connection).Options;
        var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static void SeedTransactions(ImportDbContext db)
    {
        db.CommercialTransactions.AddRange(
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-1",
                TransactionDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Empresa A",
                ProductCode = "P1",
                ProductDescription = "Produto 1",
                Quantity = 10m,
                UnitPrice = 10m,
                TotalAmount = 100m,
                TransactionType = "Venda",
                City = "Campinas",
                ProductGroup = "Grupo A",
                GrossWeightKg = 5m,
                SourceFileJobId = 1
            },
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-2",
                TransactionDate = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C2",
                CustomerName = "Empresa B",
                ProductCode = "P2",
                ProductDescription = "Produto 2",
                Quantity = 20m,
                UnitPrice = 10m,
                TotalAmount = 200m,
                TransactionType = "Bonificação",
                City = "Campinas",
                ProductGroup = "Grupo B",
                GrossWeightKg = 8m,
                SourceFileJobId = 1
            },
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-3",
                TransactionDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C3",
                CustomerName = "Empresa C",
                ProductCode = "P3",
                ProductDescription = "Produto 3",
                Quantity = 30m,
                UnitPrice = 10m,
                TotalAmount = 300m,
                TransactionType = "Venda",
                City = "Ribeirão Preto",
                ProductGroup = "Grupo A",
                GrossWeightKg = 12m,
                SourceFileJobId = 1
            },
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-4",
                TransactionDate = new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C4",
                CustomerName = "Empresa D",
                ProductCode = "P4",
                ProductDescription = "Produto 4",
                Quantity = 40m,
                UnitPrice = 10m,
                TotalAmount = 400m,
                TransactionType = "Venda",
                City = "Sorocaba",
                ProductGroup = "Grupo C",
                GrossWeightKg = 17m,
                SourceFileJobId = 1
            });

        db.SaveChanges();
    }

    private static Domain.Entities.CommercialTransaction BuildTransaction(
        string documentNumber,
        string customerName,
        string productCode,
        string productDescription,
        DateTime transactionDate,
        decimal quantity,
        decimal unitPrice)
    {
        return new Domain.Entities.CommercialTransaction
        {
            DocumentNumber = documentNumber,
            TransactionDate = transactionDate,
            CustomerCode = customerName.Replace(" ", "-", StringComparison.OrdinalIgnoreCase),
            CustomerName = customerName,
            ProductCode = productCode,
            ProductDescription = productDescription,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalAmount = quantity * unitPrice,
            TransactionType = "Venda",
            City = "Campinas",
            ProductGroup = "Grupo A",
            GrossWeightKg = quantity,
            SourceFileJobId = 1
        };
    }
}
