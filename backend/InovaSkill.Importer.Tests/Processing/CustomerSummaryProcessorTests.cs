using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class CustomerSummaryProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ShouldAggregateDailyWeeklyAndMonthlySummariesWithDistinctDailyOrders()
    {
        // Regra esperada: resumos somam receita, quantidade e peso; pedidos diários contam documentos distintos.
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.AddRange(
            Transaction("NF-1", new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc), 100m, 2m, 5m),
            Transaction("NF-1", new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc), 50m, 1m, 1m),
            Transaction("NF-2", new DateTime(2026, 5, 12, 8, 0, 0, DateTimeKind.Utc), 150m, 3m, 4m));
        await db.SaveChangesAsync();

        var processor = new CustomerSummaryProcessor(db, NullLogger<CustomerSummaryProcessor>.Instance);
        await processor.ProcessAsync(42, CancellationToken.None);

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
        // Regra esperada: reprocessar o mesmo arquivo apaga resumos antigos do arquivo e recria a mesma agregação.
        await using var db = await CreateDbAsync();
        db.CommercialTransactions.Add(Transaction("NF-1", new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc), 100m, 2m, 5m));
        await db.SaveChangesAsync();

        var processor = new CustomerSummaryProcessor(db, NullLogger<CustomerSummaryProcessor>.Instance);
        await processor.ProcessAsync(42, CancellationToken.None);
        await processor.ProcessAsync(42, CancellationToken.None);

        Assert.Single(await db.CustomerSummariesDaily.ToListAsync());
        Assert.Single(await db.CustomerSummariesWeekly.ToListAsync());
        Assert.Single(await db.CustomerSummariesMonthly.ToListAsync());
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
        DateTime transactionDate,
        decimal totalAmount,
        decimal quantity,
        decimal weight)
    {
        return new Domain.Entities.CommercialTransaction
        {
            DocumentNumber = documentNumber,
            TransactionDate = transactionDate,
            CustomerCode = "C1",
            CustomerName = "Cliente A",
            ProductCode = "P1",
            ProductDescription = "Produto 1",
            Quantity = quantity,
            UnitPrice = quantity == 0 ? 0 : totalAmount / quantity,
            TotalAmount = totalAmount,
            TransactionType = "Venda",
            City = "Campinas",
            ProductGroup = "Grupo A",
            GrossWeightKg = weight,
            SourceFileJobId = 42
        };
    }
}
