using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class SalesSummaryProcessorTests
{
    [Fact]
    public async Task ProcessAsync_RecreatesDailyAndWeeklySummariesForSameFileJobWithoutDuplication()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.CommercialTransactions.AddRange(
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-1",
                TransactionDate = new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C1",
                CustomerName = "Cliente 1",
                ProductCode = "P1",
                ProductDescription = "Produto 1",
                Quantity = 2m,
                UnitPrice = 10m,
                TotalAmount = 20m,
                TransactionType = "Venda",
                City = "Sao Paulo",
                ProductGroup = "Grupo A",
                GrossWeightKg = 1m,
                SourceFileJobId = 77
            },
            new Domain.Entities.CommercialTransaction
            {
                DocumentNumber = "NF-2",
                TransactionDate = new DateTime(2026, 5, 12, 11, 0, 0, DateTimeKind.Utc),
                CustomerCode = "C2",
                CustomerName = "Cliente 2",
                ProductCode = "P2",
                ProductDescription = "Produto 2",
                Quantity = 3m,
                UnitPrice = 5m,
                TotalAmount = 15m,
                TransactionType = "Venda",
                City = "Sao Paulo",
                ProductGroup = "Grupo A",
                GrossWeightKg = 2m,
                SourceFileJobId = 77
            });
        await db.SaveChangesAsync();

        var processor = new SalesSummaryProcessor(db, NullLogger<SalesSummaryProcessor>.Instance);
        await processor.ProcessAsync(77, CancellationToken.None);
        await processor.ProcessAsync(77, CancellationToken.None);

        var dailySummaries = await db.SalesSummariesDaily.Where(x => x.SourceFileJobId == 77).ToListAsync();
        var weeklySummaries = await db.SalesSummariesWeekly.Where(x => x.SourceFileJobId == 77).ToListAsync();

        Assert.Equal(2, dailySummaries.Count);
        Assert.Single(weeklySummaries);
        Assert.Equal(2, weeklySummaries[0].TransactionCount);
        Assert.Equal(5m, weeklySummaries[0].TotalQuantity);
        Assert.Equal(35m, weeklySummaries[0].TotalAmount);
        Assert.Equal(3m, weeklySummaries[0].TotalGrossWeightKg);
    }
}
