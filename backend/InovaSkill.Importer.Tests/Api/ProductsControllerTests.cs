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
    public async Task GetPaged_ReturnsProductsOrderedByName()
    {
        await using var db = await CreateDbAsync();
        SeedProducts(db);
        var controller = new ProductsController(db);

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
    {
        await using var db = await CreateDbAsync();
        SeedProducts(db);
        var controller = new ProductsController(db);

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
                CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                SourceFileJobId = 10
            },
            new Product
            {
                Sku = "PRD-002",
                Name = "Café Tradicional",
                Price = 12.30m,
                CreatedAt = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                SourceFileJobId = 20
            });

        db.SaveChanges();
    }
}
