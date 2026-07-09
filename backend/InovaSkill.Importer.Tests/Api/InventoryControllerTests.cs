using System.Text.Json;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class InventoryControllerTests
{
    [Fact]
    public async Task Summary_UsesCurrentImportsAndCalculatesSupportedMetrics()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"inventory-summary-{Guid.NewGuid()}").Options);
        var now = DateTime.UtcNow;
        var inventorySource = Source(InventoryCurrentImportCodes.DataSource, DataSourceImportMode.Snapshot);
        var dailySource = Source(DailyInventoryImportCodes.DataSource, DataSourceImportMode.Snapshot);
        var oldInventoryImport = Import(inventorySource, 1, now.AddDays(-2));
        var currentInventoryImport = Import(inventorySource, 2, now);
        var currentDailyImport = Import(dailySource, 1, now);
        inventorySource.CurrentImportId = currentInventoryImport.Id;
        dailySource.CurrentImportId = currentDailyImport.Id;
        var productA = Product("10000122", "6793");
        var productB = Product("10000123", "6792");
        db.AddRange(inventorySource, dailySource, oldInventoryImport, currentInventoryImport, currentDailyImport, productA, productB);
        db.InventorySnapshots.AddRange(
            Snapshot(oldInventoryImport, productA, 999, 0, 999),
            Snapshot(currentInventoryImport, productA, 10, 3, 7),
            Snapshot(currentInventoryImport, productA, 5, 5, 0, warehouseCode: "CD"),
            Snapshot(currentInventoryImport, productB, 5, 2, 0));
        db.DailyInventoryRecords.AddRange(
            Daily(currentDailyImport, productA, new DateOnly(2026, 5, 30), 100, 80, 20),
            Daily(currentDailyImport, productA, new DateOnly(2026, 5, 31), 31, 161, 26),
            Daily(currentDailyImport, productB, new DateOnly(2026, 5, 31), 50, 20, 56));
        await db.SaveChangesAsync();

        var result = await new InventoryController(db).Summary(default);
        var json = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"stockouts\":1", json);
        Assert.Contains("\"stockoutProducts\":1", json);
        Assert.Contains("\"stockoutWarehousePositions\":2", json);
        Assert.Contains("\"committedPercent\":50", json);
        Assert.Contains("\"lastDailyDate\":\"2026-05-31\"", json);
        Assert.Contains("\"lastProduction\":81", json);
        Assert.Contains("\"lastOutbound\":181", json);
        Assert.Contains("\"operationalBalance\":-100", json);
        Assert.DoesNotContain("999", json);
    }

    [Fact]
    public async Task Stockouts_ReturnsOnlyProductsWithConsolidatedUnavailableInventory()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"inventory-stockouts-{Guid.NewGuid()}").Options);
        var source = Source(InventoryCurrentImportCodes.DataSource, DataSourceImportMode.Snapshot);
        var import = Import(source, 1, DateTime.UtcNow);
        source.CurrentImportId = import.Id;
        var productWithPositiveTotal = Product("10000122", "6793");
        var stockoutProduct = Product("10000123", "6792");
        db.AddRange(source, import, productWithPositiveTotal, stockoutProduct);
        db.InventorySnapshots.AddRange(
            Snapshot(import, productWithPositiveTotal, 0, 0, 0, warehouseCode: "PA"),
            Snapshot(import, productWithPositiveTotal, 15, 1, 14, warehouseCode: "CD"),
            Snapshot(import, stockoutProduct, 3, 3, 0, warehouseCode: "PA"),
            Snapshot(import, stockoutProduct, 2, 4, -2, warehouseCode: "CD"));
        await db.SaveChangesAsync();

        var result = await new InventoryController(db).Stockouts(cancellationToken: default);
        var json = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"total\":1", json);
        Assert.Contains("\"erpCode\":\"10000123\"", json);
        Assert.Contains("\"availableQuantity\":-2", json);
        Assert.Contains("\"affectedWarehousePositions\":2", json);
        Assert.DoesNotContain("\"erpCode\":\"10000122\"", json);
    }

    [Fact]
    public async Task List_FiltersCurrentInventoryByStockoutStatus()
    {
        await using var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"inventory-list-{Guid.NewGuid()}").Options);
        var source = Source(InventoryCurrentImportCodes.DataSource, DataSourceImportMode.Snapshot);
        var import = Import(source, 1, DateTime.UtcNow);
        source.CurrentImportId = import.Id;
        var productA = Product("10000122", "6793");
        var productB = Product("10000123", "6792");
        db.AddRange(source, import, productA, productB);
        db.InventorySnapshots.AddRange(
            Snapshot(import, productA, 10, 3, 7),
            Snapshot(import, productB, 5, 5, 0));
        await db.SaveChangesAsync();

        var result = await new InventoryController(db).List(status: "STOCKOUT", cancellationToken: default);
        var json = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"total\":1", json);
        Assert.Contains("\"erpCode\":\"10000123\"", json);
        Assert.DoesNotContain("\"erpCode\":\"10000122\"", json);
    }

    private static DataSource Source(string code, DataSourceImportMode mode) => new()
    {
        Id = Guid.NewGuid(), Code = code, ProcessorKey = code, Name = code, Type = "EXCEL",
        ImportMode = mode, NextImportVersion = 1, Active = true,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static RouteImport Import(DataSource source, int version, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(), DataSourceId = source.Id, Version = version, FileName = $"{source.Code}.xlsx",
        FilePath = source.Code, Status = RouteImportStatus.Completed, CreatedAt = createdAt
    };

    private static Product Product(string erpCode, string operationalCode) => new()
    {
        Id = Guid.NewGuid(), ErpCode = erpCode, ExternalCode = erpCode, OperationalCode = operationalCode,
        Name = $"Produto {erpCode}", Description = $"Produto {erpCode}", Type = "PA", Unit = "UN",
        GroupCode = "0022", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static InventorySnapshot Snapshot(
        RouteImport import,
        Product product,
        decimal onHand,
        decimal committed,
        decimal available,
        string warehouseCode = "PA") => new()
    {
        Id = Guid.NewGuid(), ImportId = import.Id, ProductId = product.Id,
        BranchCode = "010101", WarehouseCode = warehouseCode, OnHandQuantity = onHand,
        CommittedQuantity = committed, AvailableQuantity = available, StockValue = 0,
        CommittedValue = 0, SourceRowNumber = 1, CreatedAt = DateTime.UtcNow
    };

    private static DailyInventoryRecord Daily(
        RouteImport import,
        Product product,
        DateOnly date,
        decimal production,
        decimal outbound,
        decimal closing) => new()
    {
        Id = Guid.NewGuid(), ImportId = import.Id, ProductId = product.Id,
        Date = date, ProductionQuantity = production, OutboundQuantity = outbound,
        AdjustmentQuantity = 0, ClosingQuantity = closing, SourceRowNumber = 1,
        SourceSheetName = "05.2026", CreatedAt = DateTime.UtcNow
    };
}
