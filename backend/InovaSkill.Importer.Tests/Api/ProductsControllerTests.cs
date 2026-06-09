using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class ProductsControllerTests
{
    [Fact]
    public async Task GetPaged_FiltersBySearchAcrossSkuAndName()
    {
        await using var db = await CreateDbAsync();
        SeedProducts(db);
        var controller = new ProductsController(db);

        var result = await controller.GetPaged(search: "café");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<ProductDto>>(ok.Value);
        var item = Assert.Single(payload.Items);
        Assert.Equal("PRD-003", item.Sku);
        Assert.Equal("Café Tradicional 500g", item.Name);
        Assert.Equal(18.5m, item.Price);
    }

    [Fact]
    public async Task GetPaged_AppliesPriceRangeAndStableOrdering()
    {
        await using var db = await CreateDbAsync();
        SeedProducts(db);
        var controller = new ProductsController(db);

        var result = await controller.GetPaged(priceMin: 8m, priceMax: 20m);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<ProductDto>>(ok.Value);
        Assert.Equal(2, payload.Total);
        Assert.Collection(
            payload.Items,
            first => Assert.Equal("PRD-003", first.Sku),
            second => Assert.Equal("PRD-002", second.Sku));
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

    private static void SeedProducts(ImportDbContext db)
    {
        db.Products.AddRange(
            new Product
            {
                Sku = "PRD-001",
                Name = "Arroz Tipo 1 5kg",
                Price = 27.9m,
                CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                SourceFileJobId = 10
            },
            new Product
            {
                Sku = "PRD-002",
                Name = "Feijão Carioca 1kg",
                Price = 8.7m,
                CreatedAt = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                SourceFileJobId = 11
            },
            new Product
            {
                Sku = "PRD-003",
                Name = "Café Tradicional 500g",
                Price = 18.5m,
                CreatedAt = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                SourceFileJobId = 12
            });

        db.SaveChanges();
    }
}

