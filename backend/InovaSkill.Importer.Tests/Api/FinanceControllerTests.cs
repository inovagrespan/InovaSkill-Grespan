using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class FinanceControllerTests
{
    [Fact]
    public async Task GetDashboard_ReturnsCustomersSummaryTrendRankingAndItems()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new FinanceController(db);

        var result = await controller.GetDashboard(allTime: true, revenueGranularity: "monthly", page: 1, pageSize: 20);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FinanceDashboardResponseDto>(ok.Value);

        Assert.Equal(["Cliente A", "Cliente B"], payload.Customers);
        Assert.Equal(1000m, payload.Summary.TotalRevenue);
        Assert.Equal(3, payload.Summary.TotalOrders);
        Assert.Equal(100m, payload.Summary.TotalQuantity);
        Assert.Equal(1000m / 3m, payload.Summary.AverageTicket);
        Assert.Equal(2, payload.RevenueTrend.Count);
        Assert.Equal(2, payload.CustomerRanking.Count);
        Assert.Equal(3, payload.Items.Count);
        Assert.Equal(1, payload.Page);
        Assert.Equal(20, payload.PageSize);
        Assert.Equal(3, payload.TotalItems);
        Assert.Equal(1, payload.TotalPages);
    }

    [Fact]
    public async Task GetDashboard_FiltersByCustomerAndDateRange()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new FinanceController(db);

        var result = await controller.GetDashboard(
            customer: "Cliente A",
            dateFrom: new DateTime(2026, 2, 1),
            dateTo: new DateTime(2026, 2, 28),
            allTime: false,
            revenueGranularity: "monthly",
            page: 1,
            pageSize: 20);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FinanceDashboardResponseDto>(ok.Value);

        Assert.Equal(200m, payload.Summary.TotalRevenue);
        Assert.Equal(1, payload.Summary.TotalOrders);
        Assert.Equal(20m, payload.Summary.TotalQuantity);
        var item = Assert.Single(payload.Items);
        Assert.Equal("Cliente A", item.Customer);
        Assert.Equal(200m, item.Revenue);
    }

    [Fact]
    public async Task GetDashboard_GroupsMultipleRowsOfSameDocumentIntoOneItem()
    {
        await using var db = await CreateDbAsync();
        SeedTransactions(db);
        var controller = new FinanceController(db);

        var result = await controller.GetDashboard(customer: "Cliente B", allTime: true, page: 1, pageSize: 20);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FinanceDashboardResponseDto>(ok.Value);

        var februaryItem = Assert.Single(payload.Items, x => x.Date == new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(700m, februaryItem.Revenue);
        Assert.Equal(1, februaryItem.Orders);
        Assert.Equal(70m, februaryItem.Quantity);
    }

    [Fact]
    public async Task GetDashboard_UsesPersistedTotalAmountForRawTransactions()
    {
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.Add(new Domain.Entities.CommercialTransaction
        {
            DocumentNumber = "NF-AJUSTE",
            TransactionDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            CustomerCode = "C1",
            CustomerName = "Cliente Ajuste",
            ProductCode = "P1",
            ProductDescription = "Produto com ajuste",
            Quantity = 3m,
            UnitPrice = 10m,
            TotalAmount = 25m,
            TransactionType = "Venda",
            City = "Campinas",
            ProductGroup = "Grupo A",
            GrossWeightKg = 2m,
            SourceFileJobId = 1
        });
        await db.SaveChangesAsync();

        var controller = new FinanceController(db);

        var result = await controller.GetDashboard(allTime: true, page: 1, pageSize: 20);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FinanceDashboardResponseDto>(ok.Value);
        var item = Assert.Single(payload.Items);

        Assert.Equal(25m, item.Revenue);
        Assert.Equal(25m, payload.Summary.TotalRevenue);
        Assert.Equal(25m, payload.RevenueTrend.Single().Revenue);
        Assert.Equal(25m, payload.CustomerRanking.Single().Revenue);
    }

    [Fact]
    public async Task GetDashboard_UsesDailySummariesWhenAvailable()
    {
        await using var db = await CreateDbAsync();
        db.CustomerSummariesDaily.AddRange(
            new Domain.Entities.CustomerSummaryDaily
            {
                SourceFileJobId = 1,
                ReferenceDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 2,
                Revenue = 300m,
                Quantity = 30m,
                Weight = 10m,
                ProcessedAt = DateTime.UtcNow
            },
            new Domain.Entities.CustomerSummaryDaily
            {
                SourceFileJobId = 1,
                ReferenceDate = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C2",
                CustomerName = "Cliente B",
                City = "Sorocaba",
                ProductGroup = "Grupo B",
                TransactionType = "Venda",
                Orders = 1,
                Revenue = 150m,
                Quantity = 15m,
                Weight = 5m,
                ProcessedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var controller = new FinanceController(db);

        var result = await controller.GetDashboard(allTime: true, revenueGranularity: "monthly", page: 1, pageSize: 20);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FinanceDashboardResponseDto>(ok.Value);

        Assert.Equal(["Cliente A", "Cliente B"], payload.Customers);
        Assert.Equal(450m, payload.Summary.TotalRevenue);
        Assert.Equal(3, payload.Summary.TotalOrders);
        Assert.Equal(45m, payload.Summary.TotalQuantity);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(2, payload.TotalItems);
        Assert.Equal(1, payload.TotalPages);
    }

    [Fact]
    public async Task GetDashboard_FiltersDailySummariesWithUnspecifiedDateRange()
    {
        await using var db = await CreateDbAsync();
        db.CustomerSummariesDaily.AddRange(
            new Domain.Entities.CustomerSummaryDaily
            {
                SourceFileJobId = 1,
                ReferenceDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 1,
                Revenue = 100m,
                Quantity = 10m,
                Weight = 1m,
                ProcessedAt = DateTime.UtcNow
            },
            new Domain.Entities.CustomerSummaryDaily
            {
                SourceFileJobId = 1,
                ReferenceDate = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 1,
                Revenue = 200m,
                Quantity = 20m,
                Weight = 2m,
                ProcessedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var controller = new FinanceController(db);

        var result = await controller.GetDashboard(
            dateFrom: new DateTime(2026, 2, 2),
            dateTo: new DateTime(2026, 2, 2),
            allTime: false,
            page: 1,
            pageSize: 20);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FinanceDashboardResponseDto>(ok.Value);
        var item = Assert.Single(payload.Items);

        Assert.Equal(DateTimeKind.Unspecified, new DateTime(2026, 2, 2).Kind);
        Assert.Equal(new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc), item.Date);
        Assert.Equal(200m, payload.Summary.TotalRevenue);
        Assert.Equal(1, payload.TotalItems);
    }

    [Fact]
    public async Task GetDashboard_PaginatesDetailedItems()
    {
        await using var db = await CreateDbAsync();
        db.CustomerSummariesDaily.AddRange(
            Enumerable.Range(1, 25).Select(index => new Domain.Entities.CustomerSummaryDaily
            {
                SourceFileJobId = 1,
                ReferenceDate = new DateTime(2026, 2, index, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = $"C{index}",
                CustomerName = $"Cliente {index:00}",
                City = "Campinas",
                ProductGroup = "Grupo A",
                TransactionType = "Venda",
                Orders = 1,
                Revenue = 10m * index,
                Quantity = index,
                Weight = index,
                ProcessedAt = DateTime.UtcNow
            }));
        await db.SaveChangesAsync();

        var controller = new FinanceController(db);

        var result = await controller.GetDashboard(allTime: true, page: 2, pageSize: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FinanceDashboardResponseDto>(ok.Value);

        Assert.Equal(2, payload.Page);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(25, payload.TotalItems);
        Assert.Equal(3, payload.TotalPages);
        Assert.Equal(10, payload.Items.Count);
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
                TransactionDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
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
                DocumentNumber = "NF-2",
                TransactionDate = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
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
                GrossWeightKg = 10m,
                SourceFileJobId = 1
            },
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-3",
                TransactionDate = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C2",
                CustomerName = "Cliente B",
                ProductCode = "P3",
                ProductDescription = "Produto 3",
                Quantity = 30m,
                UnitPrice = 10m,
                TotalAmount = 300m,
                TransactionType = "Venda",
                City = "Sorocaba",
                ProductGroup = "Grupo B",
                GrossWeightKg = 15m,
                SourceFileJobId = 1
            },
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-3",
                TransactionDate = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C2",
                CustomerName = "Cliente B",
                ProductCode = "P4",
                ProductDescription = "Produto 4",
                Quantity = 40m,
                UnitPrice = 10m,
                TotalAmount = 400m,
                TransactionType = "Venda",
                City = "Sorocaba",
                ProductGroup = "Grupo B",
                GrossWeightKg = 20m,
                SourceFileJobId = 1
            });

        db.SaveChanges();
    }
}
