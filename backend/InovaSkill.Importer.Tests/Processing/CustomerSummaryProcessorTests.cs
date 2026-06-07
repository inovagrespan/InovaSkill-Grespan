using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class CustomerSummaryProcessorTests
{
    private const long TargetFileJobId = 42;
    private const long OtherFileJobId = 43;

    [Fact]
    public async Task ProcessAsync_ShouldAggregateDailyWeeklyAndMonthlySummariesWithDistinctDailyOrders()
    {
        // Regra esperada: resumos somam receita, quantidade e peso; pedidos diarios contam documentos distintos.
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.AddRange(
            Transaction("NF-1", "C1", new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc), 100m, 2m, 5m, TargetFileJobId),
            Transaction("NF-1", "C1", new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc), 50m, 1m, 1m, TargetFileJobId),
            Transaction("NF-2", "C1", new DateTime(2026, 5, 12, 8, 0, 0, DateTimeKind.Utc), 150m, 3m, 4m, TargetFileJobId));
        await db.SaveChangesAsync();

        var processor = new CustomerSummaryProcessor(db, NullLogger<CustomerSummaryProcessor>.Instance);
        await processor.ProcessAsync(TargetFileJobId, CancellationToken.None);

        var daily = await db.CustomerSummariesDaily.OrderBy(x => x.ReferenceDate).ToListAsync();
        var weekly = await db.CustomerSummariesWeekly.SingleAsync();
        var monthly = await db.CustomerSummariesMonthly.SingleAsync();

        Assert.Equal(2, daily.Count);
        Assert.Equal(1, daily[0].Orders);
        Assert.Equal(150m, daily[0].Revenue);
        Assert.Equal(2, weekly.Orders);
        Assert.Equal(300m, weekly.Revenue);
        Assert.Equal(6m, weekly.Quantity);
        Assert.Equal(10m, weekly.Weight);
        Assert.Equal(new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc), weekly.WeekStartDate);
        Assert.Equal(2, monthly.Orders);
        Assert.Equal(300m, monthly.Revenue);
    }

    [Fact]
    public async Task ProcessAsync_ShouldReplaceExistingSummariesForSameFileJobWithoutDuplication()
    {
        // Regra esperada: reprocessar o mesmo arquivo apaga resumos antigos do arquivo e recria a mesma agregacao.
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.Add(Transaction("NF-1", "C1", new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc), 100m, 2m, 5m, TargetFileJobId));
        await db.SaveChangesAsync();

        var processor = new CustomerSummaryProcessor(db, NullLogger<CustomerSummaryProcessor>.Instance);
        await processor.ProcessAsync(TargetFileJobId, CancellationToken.None);
        await processor.ProcessAsync(TargetFileJobId, CancellationToken.None);

        Assert.Single(await db.CustomerSummariesDaily.ToListAsync());
        Assert.Single(await db.CustomerSummariesWeekly.ToListAsync());
        Assert.Single(await db.CustomerSummariesMonthly.ToListAsync());
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotCollapseSameDocumentAcrossDifferentCustomersOrFiles()
    {
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.AddRange(
            Transaction("NF-1", "C1", new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc), 100m, 2m, 5m, TargetFileJobId),
            Transaction("NF-1", "C2", new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc), 70m, 1m, 2m, TargetFileJobId),
            Transaction("NF-1", "C1", new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc), 999m, 9m, 9m, OtherFileJobId));
        await db.SaveChangesAsync();

        var processor = new CustomerSummaryProcessor(db, NullLogger<CustomerSummaryProcessor>.Instance);
        await processor.ProcessAsync(TargetFileJobId, CancellationToken.None);

        var daily = await db.CustomerSummariesDaily
            .OrderBy(x => x.CustomerCode)
            .ToListAsync();

        Assert.Equal(2, daily.Count);
        Assert.All(daily, summary => Assert.Equal(1, summary.Orders));
        Assert.Contains(daily, summary => summary.CustomerCode == "C1" && summary.Revenue == 100m);
        Assert.Contains(daily, summary => summary.CustomerCode == "C2" && summary.Revenue == 70m);
        Assert.DoesNotContain(daily, summary => summary.Revenue >= 999m);
    }

    [Fact]
    public async Task ProcessAsync_ShouldKeepWeeklyAndMonthlyTotalsEqualToDailyParts()
    {
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.AddRange(
            Transaction("NF-1", "C1", new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc), 100m, 2m, 5m, TargetFileJobId),
            Transaction("NF-2", "C1", new DateTime(2026, 5, 12, 9, 0, 0, DateTimeKind.Utc), 70m, 1m, 2m, TargetFileJobId),
            Transaction("NF-3", "C1", new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc), 30m, 3m, 4m, TargetFileJobId));
        await db.SaveChangesAsync();

        var processor = new CustomerSummaryProcessor(db, NullLogger<CustomerSummaryProcessor>.Instance);
        await processor.ProcessAsync(TargetFileJobId, CancellationToken.None);

        var daily = await db.CustomerSummariesDaily.ToListAsync();
        var weekly = await db.CustomerSummariesWeekly.ToListAsync();
        var monthly = await db.CustomerSummariesMonthly.SingleAsync();

        Assert.Equal(daily.Sum(x => x.Revenue), weekly.Sum(x => x.Revenue));
        Assert.Equal(daily.Sum(x => x.Quantity), weekly.Sum(x => x.Quantity));
        Assert.Equal(daily.Sum(x => x.Weight), weekly.Sum(x => x.Weight));
        Assert.Equal(daily.Sum(x => x.Revenue), monthly.Revenue);
        Assert.Equal(daily.Sum(x => x.Orders), monthly.Orders);
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

    private static Domain.Entities.CommercialTransaction Transaction(
        string documentNumber,
        string customerCode,
        DateTime transactionDate,
        decimal totalAmount,
        decimal quantity,
        decimal weight,
        long sourceFileJobId)
    {
        return new Domain.Entities.CommercialTransaction
        {
            DocumentNumber = documentNumber,
            TransactionDate = transactionDate,
            CustomerCode = customerCode,
            CustomerName = $"Cliente {customerCode}",
            ProductCode = "P1",
            ProductDescription = "Produto 1",
            Quantity = quantity,
            UnitPrice = quantity == 0 ? 0 : totalAmount / quantity,
            TotalAmount = totalAmount,
            TransactionType = "Venda",
            City = "Campinas",
            ProductGroup = "Grupo A",
            GrossWeightKg = weight,
            SourceFileJobId = sourceFileJobId
        };
    }
}
