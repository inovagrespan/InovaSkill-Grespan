using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class CustomerAnalyticsV2ControllerTests
{
    [Fact]
    public async Task GetNewCustomersMonthly_ReturnsMonthlyTimelineFromFirstPurchase()
    {
        await using var db = await CreateDbAsync();
        SeedData(db);
        var controller = new CustomerAnalyticsV2Controller(db);

        var result = await controller.GetNewCustomersMonthly(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CustomerNewCustomersMonthlyResponseDto>(ok.Value);

        Assert.Equal(3, payload.TotalNewCustomers);
        Assert.Equal(2, payload.ActiveMonths);
        Assert.Equal(3, payload.Points.Count);
        Assert.Equal(2, payload.Points[0].NewCustomers); // Jan
        Assert.Equal(0, payload.Points[1].NewCustomers); // Fev
        Assert.Equal(1, payload.Points[2].NewCustomers); // Mar
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

    private static void SeedData(ImportDbContext db)
    {
        db.CommercialTransactions.AddRange(
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-001",
                TransactionDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                ProductCode = "P1",
                ProductDescription = "Produto",
                Quantity = 1,
                UnitPrice = 10,
                TotalAmount = 10,
                TransactionType = "Venda",
                City = "SP",
                ProductGroup = "G1",
                GrossWeightKg = 1,
                SourceFileJobId = 1
            },
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-002",
                TransactionDate = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C2",
                CustomerName = "Cliente B",
                ProductCode = "P1",
                ProductDescription = "Produto",
                Quantity = 1,
                UnitPrice = 10,
                TotalAmount = 10,
                TransactionType = "Venda",
                City = "SP",
                ProductGroup = "G1",
                GrossWeightKg = 1,
                SourceFileJobId = 1
            },
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-003",
                TransactionDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C3",
                CustomerName = "Cliente C",
                ProductCode = "P2",
                ProductDescription = "Produto",
                Quantity = 1,
                UnitPrice = 10,
                TotalAmount = 10,
                TransactionType = "Venda",
                City = "SP",
                ProductGroup = "G1",
                GrossWeightKg = 1,
                SourceFileJobId = 1
            },
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-004",
                TransactionDate = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente A",
                ProductCode = "P3",
                ProductDescription = "Produto",
                Quantity = 1,
                UnitPrice = 15,
                TotalAmount = 15,
                TransactionType = "Venda",
                City = "SP",
                ProductGroup = "G2",
                GrossWeightKg = 1,
                SourceFileJobId = 2
            });

        db.SaveChanges();
    }
}
