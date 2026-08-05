using ClosedXML.Excel;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.RouteImports;

public sealed class ProductInventoryImportTests
{
    [Theory]
    [InlineData("V6793", "6793")]
    [InlineData("v6793", "6793")]
    [InlineData("  V6793  ", "6793")]
    [InlineData("6793", "6793")]
    public void NormalizeOperationalCode_RemovesOnlyOptionalVPrefix(string value, string expected)
    {
        Assert.Equal(expected, ProductCodeNormalizer.NormalizeOperationalCode(value));
    }

    [Fact]
    public async Task ProductsProcessor_CreatesAndEnrichesByErpCodeWithoutDuplicating()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        var import = await AddImportAsync(db, ProductImportCodes.DataSource, ProductImportCodes.ProcessorKey,
            DataSourceImportMode.Upsert, "products");
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(), ExternalCode = "10000122", ErpCode = "10000122",
            Description = "Antigo", Name = "Antigo", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var processor = new ProductsProcessor(db, new MemoryStorage(CreateProductsWorkbook()), new ProductsSpreadsheetParser());

        await processor.ProcessAsync(import.Id, default);
        await processor.ProcessAsync(import.Id, default);

        var product = await db.Products.SingleAsync();
        Assert.Equal("10000122", product.ErpCode);
        Assert.Equal("6793", product.OperationalCode);
        Assert.Equal("RAPIDO 80 GR", product.Name);
        Assert.Equal("PA", product.Type);
        Assert.Equal("UN", product.Unit);
        Assert.Equal("0022", product.GroupCode);
        Assert.Equal(0.8m, product.NetWeightKg);
        Assert.Equal(0.82m, product.GrossWeightKg);
        Assert.Equal("789", product.Gtin);
    }

    [Fact]
    public async Task InventoryCurrentProcessor_LinksByErpCodeAndPreservesPreviousVersions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        var source = await AddSourceAsync(db, InventoryCurrentImportCodes.DataSource,
            InventoryCurrentImportCodes.ProcessorKey, DataSourceImportMode.Snapshot);
        var oldImport = await AddImportAsync(db, source, "old-stock");
        var import = await AddImportAsync(db, source, "stock");
        var product = AddProduct(db, "10000122", "6793");
        db.InventorySnapshots.Add(new InventorySnapshot
        {
            Id = Guid.NewGuid(), ImportId = oldImport.Id, ProductId = product.Id,
            BranchCode = "010101", WarehouseCode = "PA", OnHandQuantity = 1,
            CommittedQuantity = 0, AvailableQuantity = 1, StockValue = 1,
            CommittedValue = 0, SourceRowNumber = 2, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var processor = new InventoryCurrentProcessor(db, new MemoryStorage(CreateInventoryWorkbook(includeMissingProduct: true)),
            new InventoryCurrentSpreadsheetParser());

        await processor.ProcessAsync(import.Id, default);

        Assert.Equal(2, await db.InventorySnapshots.CountAsync());
        var current = await db.InventorySnapshots.SingleAsync(x => x.ImportId == import.Id);
        Assert.Equal(product.Id, current.ProductId);
        Assert.Equal(10, current.OnHandQuantity);
        Assert.Equal(3, current.CommittedQuantity);
        Assert.Equal(7, current.AvailableQuantity);
        Assert.Equal(1, await db.RouteImportErrors.CountAsync(x => x.ImportId == import.Id));
    }

    [Fact]
    public void DailyInventorySpreadsheetParser_ParsesDailyExampleAndEmptyCellsAsZero()
    {
        using var stream = new MemoryStream(CreateDailyInventoryWorkbook(duplicateConflict: false));

        var row = new DailyInventorySpreadsheetParser().Parse(stream).Rows
            .First(x => x.OperationalCode == "6793" && x.Date == new DateOnly(2026, 5, 1));

        Assert.Equal(31, row.ProductionQuantity);
        Assert.Equal(161, row.OutboundQuantity);
        Assert.Equal(26, row.ClosingQuantity);
        Assert.Equal(156 + 31 - 161, row.ClosingQuantity);
    }

    [Fact]
    public void DailyInventorySpreadsheetParser_IgnoresSummaryBlocksAndDatesOutsideSheetMonth()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("02.2024");
        AddDailySheet(sheet, 31, new DateTime(2024, 2, 29));
        sheet.Cell(1, 9).Value = new DateTime(2024, 3, 1);
        sheet.Cell(2, 9).Value = "ENTRADA";
        sheet.Cell(2, 10).Value = "SAIDA";
        sheet.Cell(2, 11).Value = "ATUAL";
        sheet.Cell(3, 9).Value = 10;
        sheet.Cell(3, 10).Value = 2;
        sheet.Cell(3, 11).Value = 34;
        sheet.Cell(5, 1).Value = "CÓD";
        sheet.Cell(7, 1).Value = "V6793";
        sheet.Cell(7, 2).Value = "6793";
        sheet.Cell(7, 3).Value = "RAPIDO 80 GR";
        sheet.Cell(7, 5).Value = 999;
        sheet.Cell(7, 6).Value = 999;
        sheet.Cell(7, 7).Value = 999;
        sheet.Cell(7, 8).Value = 999;
        using var stream = new MemoryStream(Save(workbook));

        var result = new DailyInventorySpreadsheetParser().Parse(stream);

        var row = Assert.Single(result.Rows);
        Assert.Equal(new DateOnly(2024, 2, 29), row.Date);
        Assert.Equal(31, row.ProductionQuantity);
        Assert.DoesNotContain(result.Rows, item => item.RowNumber == 7);
        Assert.DoesNotContain(result.Rows, item => item.Date == new DateOnly(2024, 3, 1));
    }

    [Fact]
    public void DailyInventorySpreadsheetParser_PrefersCanonicalMonthlySheetOverCopy()
    {
        using var workbook = new XLWorkbook();
        AddDailySheet(workbook.AddWorksheet("06.2025 (2)"), 999, new DateTime(2025, 6, 30));
        AddDailySheet(workbook.AddWorksheet("06.2025"), 31, new DateTime(2025, 6, 30));
        using var stream = new MemoryStream(Save(workbook));

        var row = Assert.Single(new DailyInventorySpreadsheetParser().Parse(stream).Rows);

        Assert.Equal("06.2025", row.SheetName);
        Assert.Equal(31, row.ProductionQuantity);
    }

    [Fact]
    public void DailyInventorySpreadsheetParser_DetectsColumnsAndDataRowFromNewLayout()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("05.2026");
        sheet.Cell(1, 1).Value = "CÓD.";
        sheet.Cell(1, 2).Value = "CÓD.TOTVS";
        sheet.Cell(1, 3).Value = "CÓD";
        sheet.Cell(1, 4).Value = "PRODUTO";
        sheet.Cell(1, 5).Value = "PESO";
        sheet.Cell(1, 6).Value = "ATUAL";
        sheet.Cell(1, 7).Value = new DateTime(2026, 5, 1);
        sheet.Cell(2, 7).Value = "1º TURNO";
        sheet.Cell(3, 4).Value = "MOVIMENTAÇÕES";
        sheet.Cell(3, 7).Value = "ENTRADA";
        sheet.Cell(3, 8).Value = "ENTRADA";
        sheet.Cell(3, 9).Value = "SAIDA";
        sheet.Cell(3, 10).Value = "ATUAL";
        sheet.Cell(4, 1).Value = 300;
        sheet.Cell(4, 2).Value = 10000121;
        sheet.Cell(4, 3).Value = 6792;
        sheet.Cell(4, 4).Value = "RAPIDO 70 GR";
        sheet.Cell(4, 6).Value = 20;
        sheet.Cell(4, 7).Value = 2;
        sheet.Cell(4, 8).Value = 3;
        sheet.Cell(4, 9).Value = 4;
        sheet.Cell(4, 10).FormulaA1 = "F4+G4+H4-I4";
        using var stream = new MemoryStream(Save(workbook));

        var row = Assert.Single(new DailyInventorySpreadsheetParser().Parse(stream).Rows);

        Assert.Equal("6792", row.OperationalCode);
        Assert.Equal("10000121", row.ErpCode);
        Assert.Equal("RAPIDO 70 GR", row.ProductName);
        Assert.Equal(5, row.ProductionQuantity);
        Assert.Equal(4, row.OutboundQuantity);
        Assert.Equal(21, row.ClosingQuantity);
        Assert.Equal(4, row.RowNumber);
    }

    [Fact]
    public async Task DailyInventoryProcessor_PrioritizesErpCodeWhenOperationalCodeIsIncorrect()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        var import = await AddImportAsync(db, DailyInventoryImportCodes.DataSource,
            DailyInventoryImportCodes.ProcessorKey, DataSourceImportMode.Snapshot, "daily-new-layout");
        var product = AddProduct(db, "10000121", "5996");
        await db.SaveChangesAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("05.2026");
        sheet.Cell(1, 1).Value = "CÓD.";
        sheet.Cell(1, 2).Value = "CÓD.TOTVS";
        sheet.Cell(1, 3).Value = "CÓD";
        sheet.Cell(1, 4).Value = "PRODUTO";
        sheet.Cell(1, 6).Value = "ATUAL";
        sheet.Cell(1, 7).Value = new DateTime(2026, 5, 1);
        sheet.Cell(3, 4).Value = "MOVIMENTAÇÕES";
        sheet.Cell(3, 7).Value = "ENTRADA";
        sheet.Cell(3, 8).Value = "SAIDA";
        sheet.Cell(3, 9).Value = "ATUAL";
        sheet.Cell(4, 2).Value = "10000121";
        sheet.Cell(4, 3).Value = "6902";
        sheet.Cell(4, 4).Value = "Produto";
        sheet.Cell(4, 6).Value = 10;
        sheet.Cell(4, 7).Value = 2;
        sheet.Cell(4, 8).Value = 1;
        sheet.Cell(4, 9).Value = 11;
        var processor = new DailyInventoryProcessor(db, new MemoryStorage(Save(workbook)),
            new DailyInventorySpreadsheetParser());

        await processor.ProcessAsync(import.Id, default);

        var record = await db.DailyInventoryRecords.SingleAsync();
        Assert.Equal(product.Id, record.ProductId);
        Assert.Empty(await db.RouteImportErrors.Where(error => error.ImportId == import.Id).ToListAsync());
    }

    [Fact]
    public async Task DailyInventoryProcessor_UsesOperationalCodeAndRejectsConflictingDuplicateDate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        var import = await AddImportAsync(db, DailyInventoryImportCodes.DataSource,
            DailyInventoryImportCodes.ProcessorKey, DataSourceImportMode.Snapshot, "daily");
        var product = AddProduct(db, "10000122", "6793");
        await db.SaveChangesAsync();
        var processor = new DailyInventoryProcessor(db, new MemoryStorage(CreateDailyInventoryWorkbook(duplicateConflict: true)),
            new DailyInventorySpreadsheetParser());

        await processor.ProcessAsync(import.Id, default);

        _ = product;
        Assert.Empty(await db.DailyInventoryRecords.ToListAsync());
        Assert.Equal(1, await db.RouteImportErrors.CountAsync(x => x.ImportId == import.Id && x.Field == "date"));
    }

    [Fact]
    public async Task RealAttachedSpreadsheets_ProcessTogetherWhenAvailable()
    {
        var files = new[]
        {
            "/home/leonardo/Downloads/Cadastro de produtos.xlsx",
            "/home/leonardo/Downloads/Estoque - Atual v2.xlsx",
            "/home/leonardo/Downloads/v3 - 1. CONTROLE DE ESTOQUE P\u00c3ES 2026 - CORRIGIDO.xlsx"
        };
        if (files.Any(path => !File.Exists(path))) return;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        var productImport = await AddImportAsync(db, ProductImportCodes.DataSource, ProductImportCodes.ProcessorKey,
            DataSourceImportMode.Upsert, "products-real");
        await new ProductsProcessor(db, new MemoryStorage(await File.ReadAllBytesAsync(files[0])),
            new ProductsSpreadsheetParser()).ProcessAsync(productImport.Id, default);
        var inventoryImport = await AddImportAsync(db, InventoryCurrentImportCodes.DataSource,
            InventoryCurrentImportCodes.ProcessorKey, DataSourceImportMode.Snapshot, "inventory-real");
        await new InventoryCurrentProcessor(db, new MemoryStorage(await File.ReadAllBytesAsync(files[1])),
            new InventoryCurrentSpreadsheetParser()).ProcessAsync(inventoryImport.Id, default);
        var dailyImport = await AddImportAsync(db, DailyInventoryImportCodes.DataSource,
            DailyInventoryImportCodes.ProcessorKey, DataSourceImportMode.Snapshot, "daily-real");
        await new DailyInventoryProcessor(db, new MemoryStorage(await File.ReadAllBytesAsync(files[2])),
            new DailyInventorySpreadsheetParser()).ProcessAsync(dailyImport.Id, default);

        Assert.True(await db.Products.CountAsync() > 9_000);
        Assert.True(await db.InventorySnapshots.CountAsync() > 200);
        Assert.True(await db.DailyInventoryRecords.CountAsync() > 1_000);
        var rapido = await db.Products.SingleAsync(x => x.OperationalCode == "6793");
        Assert.True(await db.DailyInventoryRecords.AnyAsync(x => x.ProductId == rapido.Id));
        var pendingDateErrors = await db.RouteImportErrors
            .Where(x => x.ImportId == dailyImport.Id && x.Field == "date" && x.Status == ImportErrorStatus.Pending)
            .ToListAsync();
        Assert.True(pendingDateErrors.Count == 0, string.Join(Environment.NewLine,
            pendingDateErrors.Take(20).Select(error =>
                $"{error.SheetName}:{error.RowNumber} {error.RawValue} {error.Message}")));
        var remainingErrors = await db.RouteImportErrors.Where(error => error.ImportId == dailyImport.Id).ToListAsync();
        Assert.True(remainingErrors.Count == 0, string.Join(Environment.NewLine,
            remainingErrors.Select(error =>
                $"{error.SheetName}:{error.RowNumber} {error.Field} '{error.RawValue}' {error.Message}")));
        Assert.Equal(RouteImportStatus.Completed, (await db.RouteImports.FindAsync(dailyImport.Id))!.Status);
    }

    private static ImportDbContext CreateDb(SqliteConnection connection)
    {
        var db = new ImportDbContext(new DbContextOptionsBuilder<ImportDbContext>()
            .UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<DataSource> AddSourceAsync(
        ImportDbContext db,
        string code,
        string processorKey,
        DataSourceImportMode mode)
    {
        var source = new DataSource
        {
            Id = Guid.NewGuid(), Code = code, ProcessorKey = processorKey, Name = code,
            Type = "EXCEL", ImportMode = mode, NextImportVersion = 1, Active = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();
        return source;
    }

    private static async Task<RouteImport> AddImportAsync(
        ImportDbContext db,
        string code,
        string processorKey,
        DataSourceImportMode mode,
        string storageKey)
    {
        var source = await AddSourceAsync(db, code, processorKey, mode);
        return await AddImportAsync(db, source, storageKey);
    }

    private static async Task<RouteImport> AddImportAsync(ImportDbContext db, DataSource source, string storageKey)
    {
        var import = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = source.NextImportVersion++,
            FileName = $"{storageKey}.xlsx", FilePath = storageKey, Status = RouteImportStatus.Processing,
            CreatedAt = DateTime.UtcNow
        };
        db.RouteImports.Add(import);
        await db.SaveChangesAsync();
        return import;
    }

    private static Product AddProduct(ImportDbContext db, string erpCode, string operationalCode)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(), ErpCode = erpCode, ExternalCode = erpCode,
            OperationalCode = operationalCode, Name = "RAPIDO 80 GR", Description = "RAPIDO 80 GR",
            Type = "PA", Unit = "UN", GroupCode = "0022", CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Products.Add(product);
        return product;
    }

    private static byte[] CreateProductsWorkbook()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Listagem do Browse");
        string[] headers = ["Codigo", "*Cod.OnClick", "Descricao", "Tipo", "Unidade", "Grupo", "Peso Liquido", "Peso Bruto", "Cod. GTIN"];
        for (var index = 0; index < headers.Length; index++) sheet.Cell(2, index + 1).Value = headers[index];
        object[] values = ["10000122", " V6793 ", "RAPIDO 80 GR", "PA", "UN", "0022", 0.8, 0.82, "789"];
        for (var index = 0; index < values.Length; index++) sheet.Cell(3, index + 1).Value = XLCellValue.FromObject(values[index]);
        return Save(workbook);
    }

    private static byte[] CreateInventoryWorkbook(bool includeMissingProduct)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("1-Saldos em Estoque");
        string[] headers = ["CODIGO", "FL", "ARMZ", "SALDO EM ESTOQUE", "EMPENHO PARA REQ/PV/RESERVA",
            "ESTOQUE DISPONIVEL", "VALOR EM ESTOQUE", "VALOR EMPENHADO"];
        for (var index = 0; index < headers.Length; index++) sheet.Cell(2, index + 1).Value = headers[index];
        object[] values = ["10000122", "010101", "PA", 10, 3, 7, 100, 30];
        for (var index = 0; index < values.Length; index++) sheet.Cell(3, index + 1).Value = XLCellValue.FromObject(values[index]);
        if (includeMissingProduct)
        {
            sheet.Cell(4, 1).Value = "404";
            sheet.Cell(4, 2).Value = "010101";
            sheet.Cell(4, 3).Value = "PA";
        }
        return Save(workbook);
    }

    private static byte[] CreateDailyInventoryWorkbook(bool duplicateConflict)
    {
        using var workbook = new XLWorkbook();
        AddDailySheet(workbook.AddWorksheet("Controle A"), 31);
        AddDailySheet(workbook.AddWorksheet("Controle B"), duplicateConflict ? 30 : 31);
        return Save(workbook);
    }

    private static void AddDailySheet(
        IXLWorksheet sheet,
        int production,
        DateTime? date = null)
    {
        sheet.Cell(1, 1).Value = "CÓD.";
        sheet.Cell(1, 2).Value = "CÓD";
        sheet.Cell(1, 3).Value = "PRODUTO";
        sheet.Cell(1, 5).Value = "ATUAL";
        sheet.Cell(1, 6).Value = date ?? new DateTime(2026, 5, 1);
        sheet.Cell(2, 3).Value = "MOVIMENTAÇÕES";
        sheet.Cell(2, 6).Value = "ENTRADA";
        sheet.Cell(2, 7).Value = "SAIDA";
        sheet.Cell(2, 8).Value = "ATUAL";
        sheet.Cell(3, 1).Value = "V6793";
        sheet.Cell(3, 2).Value = "6793";
        sheet.Cell(3, 3).Value = "RAPIDO 80 GR";
        sheet.Cell(3, 5).Value = 156;
        sheet.Cell(3, 6).Value = production;
        sheet.Cell(3, 7).Value = 161;
        sheet.Cell(3, 8).FormulaA1 = "E3+F3-G3";
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed class MemoryStorage(byte[] bytes) : IImportFileStorage
    {
        public Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}
