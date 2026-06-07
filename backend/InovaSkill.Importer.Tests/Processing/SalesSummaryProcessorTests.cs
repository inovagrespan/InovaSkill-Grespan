using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class SalesSummaryProcessorTests
{
    private const long TargetFileJobId = 77;
    private const long OtherFileJobId = 88;

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
            Transaction("NF-1", new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc), 2m, 10m, "Sao Paulo", "Grupo A", "Venda", 1m, TargetFileJobId),
            Transaction("NF-2", new DateTime(2026, 5, 12, 11, 0, 0, DateTimeKind.Utc), 3m, 5m, "Sao Paulo", "Grupo A", "Venda", 2m, TargetFileJobId));
        await db.SaveChangesAsync();

        var processor = new SalesSummaryProcessor(db, NullLogger<SalesSummaryProcessor>.Instance);
        await processor.ProcessAsync(TargetFileJobId, CancellationToken.None);
        await processor.ProcessAsync(TargetFileJobId, CancellationToken.None);

        var dailySummaries = await db.SalesSummariesDaily.Where(x => x.SourceFileJobId == TargetFileJobId).ToListAsync();
        var weeklySummaries = await db.SalesSummariesWeekly.Where(x => x.SourceFileJobId == TargetFileJobId).ToListAsync();

        Assert.Equal(2, dailySummaries.Count);
        Assert.Single(weeklySummaries);
        Assert.Equal(2, weeklySummaries[0].TransactionCount);
        Assert.Equal(5m, weeklySummaries[0].TotalQuantity);
        Assert.Equal(35m, weeklySummaries[0].TotalAmount);
        Assert.Equal(3m, weeklySummaries[0].TotalGrossWeightKg);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresOtherFileJobsAndKeepsDimensionBoundaries()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.CommercialTransactions.AddRange(
            Transaction("NF-10", new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc), 2m, 10m, "Campinas", "Grupo A", "Venda", 3m, TargetFileJobId),
            Transaction("NF-11", new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc), 1m, 90m, "Campinas", "Grupo B", "Venda", 7m, TargetFileJobId),
            Transaction("NF-12", new DateTime(2026, 5, 11, 11, 0, 0, DateTimeKind.Utc), 1m, 5m, "Campinas", "Grupo A", "Devolucao", 1m, TargetFileJobId),
            Transaction("NF-99", new DateTime(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc), 9m, 999m, "Campinas", "Grupo A", "Venda", 9m, OtherFileJobId));
        await db.SaveChangesAsync();

        var processor = new SalesSummaryProcessor(db, NullLogger<SalesSummaryProcessor>.Instance);
        await processor.ProcessAsync(TargetFileJobId, CancellationToken.None);

        var daily = await db.SalesSummariesDaily
            .Where(x => x.SourceFileJobId == TargetFileJobId)
            .OrderBy(x => x.ProductGroup)
            .ThenBy(x => x.TransactionType)
            .ToListAsync();

        Assert.Equal(3, daily.Count);
        Assert.DoesNotContain(daily, summary => summary.TotalAmount >= 999m);
        Assert.Contains(daily, summary => summary.ProductGroup == "Grupo A" && summary.TransactionType == "Venda" && summary.TotalAmount == 20m);
        Assert.Contains(daily, summary => summary.ProductGroup == "Grupo A" && summary.TransactionType == "Devolucao" && summary.TotalAmount == 5m);
        Assert.Contains(daily, summary => summary.ProductGroup == "Grupo B" && summary.TransactionType == "Venda" && summary.TotalAmount == 90m);
    }

    [Fact]
    public async Task ProcessAsync_GroupsSundayIntoPreviousBusinessWeek()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.CommercialTransactions.AddRange(
            Transaction("NF-SUN", new DateTime(2026, 5, 17, 23, 30, 0, DateTimeKind.Utc), 1m, 100m, "Santos", "Grupo A", "Venda", 1m, TargetFileJobId),
            Transaction("NF-MON", new DateTime(2026, 5, 18, 0, 30, 0, DateTimeKind.Utc), 1m, 200m, "Santos", "Grupo A", "Venda", 1m, TargetFileJobId));
        await db.SaveChangesAsync();

        var processor = new SalesSummaryProcessor(db, NullLogger<SalesSummaryProcessor>.Instance);
        await processor.ProcessAsync(TargetFileJobId, CancellationToken.None);

        var weekly = await db.SalesSummariesWeekly
            .OrderBy(x => x.WeekStartDate)
            .ToListAsync();

        Assert.Equal(2, weekly.Count);
        Assert.Equal(new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc), weekly[0].WeekStartDate);
        Assert.Equal(100m, weekly[0].TotalAmount);
        Assert.Equal(new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc), weekly[1].WeekStartDate);
        Assert.Equal(200m, weekly[1].TotalAmount);
    }

    private static Domain.Entities.CommercialTransaction Transaction(
        string documentNumber,
        DateTime transactionDate,
        decimal quantity,
        decimal unitPrice,
        string city,
        string productGroup,
        string transactionType,
        decimal grossWeightKg,
        long sourceFileJobId)
    {
        return new Domain.Entities.CommercialTransaction
        {
            DocumentNumber = documentNumber,
            TransactionDate = transactionDate,
            CustomerCode = $"C-{documentNumber}",
            CustomerName = $"Cliente {documentNumber}",
            ProductCode = $"P-{documentNumber}",
            ProductDescription = $"Produto {documentNumber}",
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalAmount = quantity * unitPrice,
            TransactionType = transactionType,
            City = city,
            ProductGroup = productGroup,
            GrossWeightKg = grossWeightKg,
            SourceFileJobId = sourceFileJobId
        };
    }
}
