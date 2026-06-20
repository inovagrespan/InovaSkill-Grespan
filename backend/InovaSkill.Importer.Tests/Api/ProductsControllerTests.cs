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
<<<<<<< HEAD
    public async Task GetPaged_FiltersBySearchAcrossSkuAndName()
=======
    public async Task GetPaged_ReturnsProductsOrderedByName()
>>>>>>> 63b18f765086c6de4ac2dbaf716dcfa70e776cc1
    {
        await using var db = await CreateDbAsync();
        SeedProducts(db);
        var controller = new ProductsController(db);

<<<<<<< HEAD
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
=======
        var result = await controller.GetPaged(page: 1, pageSize: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<ProductDto>>(ok.Value);
        Assert.Equal(3, payload.Total);
        Assert.Collection(
            payload.Items,
            first => Assert.Equal("Café Tradicional", first.Name),
            second => Assert.Equal("Massa Congelada", second.Name),
            third => Assert.Equal("Pão Francês", third.Name));
    }

    [Fact]
    public async Task GetPaged_SearchesBySkuOrNameIgnoringCase()
>>>>>>> 63b18f765086c6de4ac2dbaf716dcfa70e776cc1
    {
        await using var db = await CreateDbAsync();
        SeedProducts(db);
        var controller = new ProductsController(db);

<<<<<<< HEAD
        var result = await controller.GetPaged(priceMin: 8m, priceMax: 20m);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<ProductDto>>(ok.Value);
        Assert.Equal(2, payload.Total);
        Assert.Collection(
            payload.Items,
            first => Assert.Equal("PRD-003", first.Sku),
            second => Assert.Equal("PRD-002", second.Sku));
=======
        var bySku = await controller.GetPaged(search: "prd-002");
        var skuPayload = Assert.IsType<PagedResult<ProductDto>>(Assert.IsType<OkObjectResult>(bySku.Result).Value);
        var skuItem = Assert.Single(skuPayload.Items);
        Assert.Equal("PRD-002", skuItem.Sku);

        var byName = await controller.GetPaged(search: "FRANC");
        var namePayload = Assert.IsType<PagedResult<ProductDto>>(Assert.IsType<OkObjectResult>(byName.Result).Value);
        var nameItem = Assert.Single(namePayload.Items);
        Assert.Equal("Pão Francês", nameItem.Name);
    }

    [Fact]
    public async Task GetPaged_ClampsPageSize()
    {
        await using var db = await CreateDbAsync();
        SeedProducts(db);
        var controller = new ProductsController(db);

        var result = await controller.GetPaged(pageSize: 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResult<ProductDto>>(ok.Value);
        Assert.Equal(10, payload.PageSize);
>>>>>>> 63b18f765086c6de4ac2dbaf716dcfa70e776cc1
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
<<<<<<< HEAD
                Sku = "PRD-001",
                Name = "Arroz Tipo 1 5kg",
                Price = 27.9m,
=======
                Sku = "PRD-003",
                Name = "Pão Francês",
                Price = 19.90m,
                CreatedAt = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                SourceFileJobId = 30
            },
            new Product
            {
                Sku = "PRD-001",
                Name = "Massa Congelada",
                Price = 42.50m,
>>>>>>> 63b18f765086c6de4ac2dbaf716dcfa70e776cc1
                CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                SourceFileJobId = 10
            },
            new Product
            {
                Sku = "PRD-002",
<<<<<<< HEAD
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
=======
                Name = "Café Tradicional",
                Price = 12.30m,
                CreatedAt = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                SourceFileJobId = 20
>>>>>>> 63b18f765086c6de4ac2dbaf716dcfa70e776cc1
            });

        db.SaveChanges();
    }
}
<<<<<<< HEAD

=======
>>>>>>> 63b18f765086c6de4ac2dbaf716dcfa70e776cc1
