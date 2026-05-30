using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing.Buffers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Processing;

public sealed class ProductBufferTests
{
    [Fact]
    public async Task FlushAsync_IgnoresDuplicateSku_InBufferAndDatabase()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ImportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Products.Add(new Product
        {
            Sku = "SKU-EXISTENTE",
            Name = "Produto Existente",
            Price = 10m,
            CreatedAt = DateTime.UtcNow,
            SourceFileJobId = 1
        });
        await db.SaveChangesAsync();

        var buffer = new ProductBuffer();
        buffer.Add(new ImportedRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sku"] = "SKU-001",
            ["name"] = "Produto A",
            ["price"] = "12.5",
            ["createdat"] = "2026-01-01"
        }), 10);
        buffer.Add(new ImportedRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sku"] = "SKU-001",
            ["name"] = "Produto A Duplicado",
            ["price"] = "12.5",
            ["createdat"] = "2026-01-02"
        }), 10);
        buffer.Add(new ImportedRow(3, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sku"] = "SKU-EXISTENTE",
            ["name"] = "Produto Existente Novo",
            ["price"] = "99",
            ["createdat"] = "2026-01-03"
        }), 10);

        await buffer.FlushAsync(db, CancellationToken.None);
        var products = await db.Products.OrderBy(x => x.Sku).ToListAsync();

        Assert.Equal(2, products.Count);
        Assert.Contains(products, x => x.Sku == "SKU-001");
        Assert.Contains(products, x => x.Sku == "SKU-EXISTENTE");
    }
}
