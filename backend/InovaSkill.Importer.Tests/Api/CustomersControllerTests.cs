using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class CustomersControllerTests
{
    [Fact]
    public async Task GetSummary_ReturnsAggregatedValues()
    {
        await using var db = await CreateDbAsync();
        SeedBaseData(db);
        var controller = new CustomersController(db);

        var result = await controller.GetSummary("C1", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CustomerSummaryResponseDto>(ok.Value);

        Assert.Equal("C1", payload.CustomerCode);
        Assert.Equal(300m, payload.TotalRevenue);
        Assert.Equal(2, payload.TotalOrders);
        Assert.Equal(150m, payload.AverageTicket);
        Assert.Equal(300m, payload.AverageRevenueMonthly);
        Assert.Equal(150m, payload.AverageRevenueWeekly);
        Assert.Equal("Em queda", payload.Status);
    }

    [Fact]
    public async Task GetSummary_ReturnsSinglePurchaseMessageInputs_WhenOnlyOnePurchaseInPeriod()
    {
        await using var db = await CreateDbAsync();
        SeedBaseData(db);
        var controller = new CustomersController(db);

        var result = await controller.GetSummary("C1", new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CustomerSummaryResponseDto>(ok.Value);

        Assert.Equal(100m, payload.TotalRevenue);
        Assert.Equal(100m, payload.AverageRevenueMonthly);
        Assert.Equal(100m, payload.AverageRevenueWeekly);
        Assert.Equal(100m, payload.AverageTicket);
        Assert.Null(payload.AverageDaysBetweenPurchases);
    }

    [Fact]
    public async Task GetTimeline_ReturnsMetricValueForRequestedGranularity()
    {
        await using var db = await CreateDbAsync();
        SeedBaseData(db);
        var controller = new CustomersController(db);

        var result = await controller.GetTimeline("C1", "monthly", "quantity", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CustomerTimelineResponseDto>(ok.Value);

        Assert.Equal("monthly", payload.Granularity);
        Assert.Equal("quantity", payload.Metric);
        Assert.NotEmpty(payload.Points);
        Assert.All(payload.Points, x => Assert.Equal(x.Quantity, x.Value));
    }

    [Fact]
    public async Task GetComparison_ReturnsThreePeriods()
    {
        await using var db = await CreateDbAsync();
        SeedBaseData(db);
        var controller = new CustomersController(db);

        var result = await controller.GetComparison("C1", new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CustomerComparisonResponseDto>(ok.Value);

        Assert.Equal(3, payload.Items.Count);
        Assert.Contains(payload.Items, x => x.Label.Contains("mês", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(payload.Items, x => x.Label.Contains("semana", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(payload.Items, x => x.Label.Contains("30 dias", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetPurchaseHistory_AppliesPagination()
    {
        await using var db = await CreateDbAsync();
        SeedBaseData(db);
        var controller = new CustomersController(db);

        var result = await controller.GetPurchaseHistory("C1", page: 1, pageSize: 10, dateFrom: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), dateTo: new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CustomerPurchaseHistoryResponseDto>(ok.Value);

        Assert.Equal(1, payload.Page);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(2, payload.TotalItems);
        Assert.Equal(2, payload.Items.Count);
    }

    [Fact]
    public async Task GetSummary_ReturnsOkWithZeroValues_WhenCustomerExistsButPeriodHasNoTransactions()
    {
        await using var db = await CreateDbAsync();
        SeedBaseData(db);
        var controller = new CustomersController(db);

        var result = await controller.GetSummary("C1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CustomerSummaryResponseDto>(ok.Value);

        Assert.Equal("C1", payload.CustomerCode);
        Assert.Equal(0m, payload.TotalRevenue);
        Assert.Equal(0, payload.TotalOrders);
        Assert.Null(payload.AverageTicket);
        Assert.Null(payload.AverageRevenueMonthly);
        Assert.Null(payload.AverageRevenueWeekly);
        Assert.NotNull(payload.LastPurchaseDate);
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

    private static void SeedBaseData(ImportDbContext db)
    {
        db.Customers.Add(new Domain.Entities.Customer
        {
            CustomerCode = "C1",
            Name = "Empresa Alpha",
            Email = "alpha@empresa.com",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SourceFileJobId = 1
        });

        db.CommercialTransactions.AddRange(
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-100",
                TransactionDate = new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
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
                DocumentNumber = "NF-101",
                TransactionDate = new DateTime(2026, 5, 19, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                ProductCode = "P2",
                ProductDescription = "Produto 2",
                Quantity = 20m,
                UnitPrice = 10m,
                TotalAmount = 200m,
                TransactionType = "Venda",
                City = "Campinas",
                ProductGroup = "Grupo A",
                GrossWeightKg = 9m,
                SourceFileJobId = 1
            });

        db.CustomerSummariesWeekly.AddRange(
            new Domain.Entities.CustomerSummaryWeekly
            {
                SourceFileJobId = 1,
                WeekStartDate = new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 1,
                Revenue = 100m,
                Quantity = 10m,
                Weight = 5m,
                ProcessedAt = DateTime.UtcNow
            },
            new Domain.Entities.CustomerSummaryWeekly
            {
                SourceFileJobId = 1,
                WeekStartDate = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 1,
                Revenue = 200m,
                Quantity = 20m,
                Weight = 9m,
                ProcessedAt = DateTime.UtcNow
            });

        db.CustomerSummariesMonthly.AddRange(
            new Domain.Entities.CustomerSummaryMonthly
            {
                SourceFileJobId = 1,
                MonthStartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 0,
                Revenue = 50m,
                Quantity = 5m,
                Weight = 2m,
                ProcessedAt = DateTime.UtcNow
            },
            new Domain.Entities.CustomerSummaryMonthly
            {
                SourceFileJobId = 1,
                MonthStartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 2,
                Revenue = 300m,
                Quantity = 30m,
                Weight = 14m,
                ProcessedAt = DateTime.UtcNow
            });

        db.CustomerSummariesDaily.AddRange(
            new Domain.Entities.CustomerSummaryDaily
            {
                SourceFileJobId = 1,
                ReferenceDate = new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 1,
                Revenue = 100m,
                Quantity = 10m,
                Weight = 5m,
                ProcessedAt = DateTime.UtcNow
            },
            new Domain.Entities.CustomerSummaryDaily
            {
                SourceFileJobId = 1,
                ReferenceDate = new DateTime(2026, 5, 19, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 1,
                Revenue = 200m,
                Quantity = 20m,
                Weight = 9m,
                ProcessedAt = DateTime.UtcNow
            });

        db.SaveChanges();
    }
}
