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
        var item = Assert.Single(payload.Items);
        Assert.Equal("P1", item.ProductCode);
        Assert.Equal("Grupo A", item.ProductGroup);
        Assert.Equal("Venda", item.TransactionType);
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
        Assert.Equal(1, payload.TotalRecords);
        Assert.Equal(100m, payload.TotalAmount);
        Assert.Single(payload.Items);
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
            });

        db.SaveChanges();
    }
}
