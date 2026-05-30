using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing.Buffers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class OrderBufferTests
{
    [Fact]
    public async Task FlushAsync_IgnoresDuplicateBusinessKey_InBufferAndDatabase()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var orderedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Orders.Add(new Order
        {
            OrderNumber = "PED-EXISTENTE",
            CustomerEmail = "cliente@empresa.com",
            ProductSku = "SKU-EX",
            Quantity = 1,
            OrderedAt = orderedAt,
            SourceFileJobId = 1
        });
        await db.SaveChangesAsync();

        var buffer = new OrderBuffer();
        buffer.Add(new ImportedRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ordernumber"] = "PED-001",
            ["customeremail"] = "novo@empresa.com",
            ["productsku"] = "SKU-001",
            ["quantity"] = "2",
            ["orderedat"] = "2026-01-02"
        }), 10);
        buffer.Add(new ImportedRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ordernumber"] = "PED-001",
            ["customeremail"] = "novo@empresa.com",
            ["productsku"] = "SKU-001",
            ["quantity"] = "2",
            ["orderedat"] = "2026-01-02"
        }), 10);
        buffer.Add(new ImportedRow(3, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ordernumber"] = "PED-EXISTENTE",
            ["customeremail"] = "cliente@empresa.com",
            ["productsku"] = "SKU-EX",
            ["quantity"] = "9",
            ["orderedat"] = "2026-01-01"
        }), 10);

        await buffer.FlushAsync(db, CancellationToken.None);
        var orders = await db.Orders.OrderBy(x => x.OrderNumber).ThenBy(x => x.ProductSku).ToListAsync();

        Assert.Equal(2, orders.Count);
        Assert.Contains(orders, x => x.OrderNumber == "PED-001");
        Assert.Contains(orders, x => x.OrderNumber == "PED-EXISTENTE");
    }
}
