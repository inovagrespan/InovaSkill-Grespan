using System.Text.Json;
using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Tests.Api;

public sealed class BusinessChatToolsTests
{
    [Fact]
    public async Task SearchCustomers_ReturnsSafeCurrentCustomerData()
    {
        await using var db = CreateDbContext();
        var (_, customerId, _) = await SeedCustomerAsync(db, "0001", "01", "Padaria Marília");
        var tool = new SearchCustomersChatTool(
            new BusinessChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<SearchCustomersChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"searchTerm":"Marília","limit":10}""",
            Context(),
            default);
        var json = Serialize(result.Payload);

        Assert.True(result.Success);
        Assert.Equal(customerId, json.RootElement[0].GetProperty("id").GetGuid());
        Assert.Equal("0001", json.RootElement[0].GetProperty("code").GetString());
        Assert.False(json.RootElement[0].TryGetProperty("documentNumber", out _));
    }

    [Fact]
    public async Task GetCustomerConsumptionSummary_ReturnsBusinessMetricsAndTimeline()
    {
        await using var db = CreateDbContext();
        var (dataSourceId, customerId, municipalityId) = await SeedCustomerAsync(db, "0002", "01", "Mercado Alfa");
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "100", new DateOnly(2026, 7, 10), FiscalMovementCategory.Sale, 120m, 10m);
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "101", new DateOnly(2026, 6, 10), FiscalMovementCategory.Sale, 80m, 8m);
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "102", new DateOnly(2026, 7, 12), FiscalMovementCategory.Return, 12m, 0m);
        var tool = new GetCustomerConsumptionSummaryChatTool(
            new BusinessChatQueryService(db),
            NullLogger<GetCustomerConsumptionSummaryChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            $$"""{"customerId":"{{customerId}}","referenceDate":"2026-07-18"}""",
            Context(),
            default);
        var root = Serialize(result.Payload).RootElement;
        var metrics = root.GetProperty("summary").GetProperty("metrics");

        Assert.True(root.GetProperty("found").GetBoolean());
        Assert.Equal(120m, metrics.GetProperty("salesWeightLast30Days").GetDecimal());
        Assert.Equal(80m, metrics.GetProperty("salesWeightPrevious30Days").GetDecimal());
        Assert.Equal(50.0m, metrics.GetProperty("variationPercentage").GetDecimal());
        Assert.Equal("COMPARABLE", metrics.GetProperty("variationStatus").GetString());
        Assert.Equal("2026-07-10", metrics.GetProperty("lastPurchaseDate").GetString());
        Assert.Equal(12, root.GetProperty("summary").GetProperty("monthlyTimeline").GetArrayLength());
    }

    [Fact]
    public async Task ListRecentFiscalDocuments_FiltersByCategoryAndLimit()
    {
        await using var db = CreateDbContext();
        var (dataSourceId, customerId, municipalityId) = await SeedCustomerAsync(db, "0003", "01", "Cliente Fiscal");
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "200", new DateOnly(2026, 7, 10), FiscalMovementCategory.Sale, 100m, 5m);
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "201", new DateOnly(2026, 7, 11), FiscalMovementCategory.Return, 15m, 0m);
        var tool = new ListRecentFiscalDocumentsChatTool(
            new BusinessChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<ListRecentFiscalDocumentsChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"searchTerm":null,"operationCategory":"Return","dateFrom":"2026-07-01","dateTo":"2026-07-31","customerId":null,"limit":10}""",
            Context(),
            default);
        var json = Serialize(result.Payload);

        Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("201", json.RootElement[0].GetProperty("documentNumber").GetString());
        Assert.Equal("Return", json.RootElement[0].GetProperty("operationCategory").GetString());
        Assert.Equal(0m, json.RootElement[0].GetProperty("pricingItems")[0].GetProperty("calculatedAmount").GetDecimal());
        Assert.Equal("5102", json.RootElement[0].GetProperty("pricingItems")[0].GetProperty("cfopCode").GetString());
        Assert.Equal("501", json.RootElement[0].GetProperty("pricingItems")[0].GetProperty("tesCode").GetString());
        Assert.Equal("PED-1", json.RootElement[0].GetProperty("pricingItems")[0].GetProperty("orderNumber").GetString());
        Assert.Equal(1.5m, json.RootElement[0].GetProperty("pricingItems")[0].GetProperty("expenses").GetDecimal());
    }

    [Fact]
    public async Task ListRecentFiscalDocuments_ReturnsPricingWithSourceTotalPrecedenceAndSafeFallbacks()
    {
        await using var db = CreateDbContext();
        var (dataSourceId, customerId, municipalityId) = await SeedCustomerAsync(db, "0033", "01", "Cliente Preço");
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "501", new DateOnly(2026, 7, 10), FiscalMovementCategory.Sale, 10m, 5m, quantity: 10m, sourceTotalValue: 42m);
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "502", new DateOnly(2026, 7, 11), FiscalMovementCategory.Sale, 10m, 4m, quantity: 3m);
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "503", new DateOnly(2026, 7, 12), FiscalMovementCategory.Sale, 10m, null);
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "504", new DateOnly(2026, 7, 13), FiscalMovementCategory.Return, 10m, -3m, quantity: 2m);
        var tool = new ListRecentFiscalDocumentsChatTool(
            new BusinessChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<ListRecentFiscalDocumentsChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"searchTerm":null,"operationCategory":null,"dateFrom":null,"dateTo":null,"customerId":null,"limit":10}""",
            Context(),
            default);
        var documents = Serialize(result.Payload).RootElement.EnumerateArray()
            .ToDictionary(item => item.GetProperty("documentNumber").GetString()!, item => item.GetProperty("pricingItems")[0]);

        Assert.Equal(42m, documents["501"].GetProperty("calculatedAmount").GetDecimal());
        Assert.Equal(42m, documents["501"].GetProperty("sourceTotalValue").GetDecimal());
        Assert.Equal(12m, documents["502"].GetProperty("calculatedAmount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, documents["503"].GetProperty("calculatedAmount").ValueKind);
        Assert.Equal(-6m, documents["504"].GetProperty("calculatedAmount").GetDecimal());
    }

    [Fact]
    public async Task GetFiscalReturnRate_CalculatesRoundedRateAndZeroBase()
    {
        await using var db = CreateDbContext();
        var (dataSourceId, customerId, municipalityId) = await SeedCustomerAsync(db, "0004", "01", "Cliente Devolução");
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "300", new DateOnly(2026, 7, 10), FiscalMovementCategory.Sale, 200m, 5m);
        await SeedFiscalDocumentAsync(db, dataSourceId, customerId, municipalityId, "301", new DateOnly(2026, 7, 12), FiscalMovementCategory.Return, 25m, 0m);
        var tool = new GetFiscalReturnRateChatTool(
            new BusinessChatQueryService(db),
            NullLogger<GetFiscalReturnRateChatTool>.Instance);

        var result = await tool.ExecuteAsync("""{"periodDays":30,"dateTo":"2026-07-18"}""", Context(), default);
        var json = Serialize(result.Payload).RootElement;

        Assert.Equal(200m, json.GetProperty("salesWeightKg").GetDecimal());
        Assert.Equal(25m, json.GetProperty("returnWeightKg").GetDecimal());
        Assert.Equal(12.5m, json.GetProperty("returnRatePercent").GetDecimal());
    }

    [Fact]
    public async Task SearchProducts_ReturnsCurrentInventorySummary()
    {
        await using var db = CreateDbContext();
        var product = await SeedProductWithInventoryAsync(db, "PA001", "Produto Acabado", 50m, 10m, 40m);
        var tool = new SearchProductsChatTool(
            new BusinessChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<SearchProductsChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"searchTerm":"7891234567890","limit":10}""",
            Context(),
            default);
        var json = Serialize(result.Payload);

        Assert.Equal(product.Id, json.RootElement[0].GetProperty("id").GetGuid());
        Assert.Equal("PA001", json.RootElement[0].GetProperty("externalCode").GetString());
        Assert.Equal("Produto Acabado - descrição comercial", json.RootElement[0].GetProperty("description").GetString());
        Assert.Equal("7891234567890", json.RootElement[0].GetProperty("gtin").GetString());
        Assert.Equal(40m, json.RootElement[0].GetProperty("inventory").GetProperty("availableQuantity").GetDecimal());
        Assert.Equal(100m, json.RootElement[0].GetProperty("inventory").GetProperty("committedValue").GetDecimal());
    }

    [Fact]
    public async Task GetProductDetails_ReturnsInventoryProductionAndFiscalHistory()
    {
        await using var db = CreateDbContext();
        var (dataSourceId, customerId, municipalityId) = await SeedCustomerAsync(db, "0005", "01", "Cliente Produto");
        var product = await SeedProductWithInventoryAsync(db, "DET", "Produto Detalhado", 80m, 20m, 60m);
        await SeedDailyInventoryAsync(db, product.Id, new DateOnly(2026, 7, 16), 30m, 12m);
        await SeedFiscalDocumentAsync(
            db,
            dataSourceId,
            customerId,
            municipalityId,
            "400",
            new DateOnly(2026, 7, 15),
            FiscalMovementCategory.Sale,
            18m,
            7m,
            product.Id,
            sourceTotalValue: 55m);
        var tool = new GetProductDetailsChatTool(
            new BusinessChatQueryService(db),
            NullLogger<GetProductDetailsChatTool>.Instance);

        var result = await tool.ExecuteAsync($$"""{"productId":"{{product.Id}}"}""", Context(), default);
        var root = Serialize(result.Payload).RootElement;
        var details = root.GetProperty("details");

        Assert.True(root.GetProperty("found").GetBoolean());
        Assert.Equal("DET", details.GetProperty("product").GetProperty("erpCode").GetString());
        Assert.Equal("Produto Detalhado - descrição comercial", details.GetProperty("product").GetProperty("description").GetString());
        Assert.Single(details.GetProperty("latestInventory").EnumerateArray());
        Assert.Equal(200m, details.GetProperty("latestInventory")[0].GetProperty("committedValue").GetDecimal());
        Assert.Single(details.GetProperty("productionHistory").EnumerateArray());
        Assert.Single(details.GetProperty("recentFiscalItems").EnumerateArray());
        Assert.Equal(55m, details.GetProperty("recentFiscalItems")[0].GetProperty("sourceTotalValue").GetDecimal());
        Assert.Equal(55m, details.GetProperty("recentFiscalItems")[0].GetProperty("calculatedAmount").GetDecimal());
        Assert.Equal("5102", details.GetProperty("recentFiscalItems")[0].GetProperty("cfopCode").GetString());
    }

    [Fact]
    public async Task ListInventoryPositions_FiltersByProductAndStatus()
    {
        await using var db = CreateDbContext();
        var available = await SeedProductWithInventoryAsync(db, "OK", "Produto OK", 50m, 5m, 45m);
        await SeedProductWithInventoryAsync(db, "BAD", "Produto Ruim", 10m, 20m, -10m);
        var tool = new ListInventoryPositionsChatTool(
            new BusinessChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<ListInventoryPositionsChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            $$"""{"searchTerm":null,"productId":"{{available.Id}}","warehouse":null,"status":"AVAILABLE","sort":"committed_percent_desc","limit":10}""",
            Context(),
            default);
        var json = Serialize(result.Payload);

        Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(available.Id, json.RootElement[0].GetProperty("productId").GetGuid());
        Assert.Equal(10.0m, json.RootElement[0].GetProperty("committedPercent").GetDecimal());
        Assert.Equal(50m, json.RootElement[0].GetProperty("committedValue").GetDecimal());
    }

    [Fact]
    public async Task InventorySummary_AndStockouts_UseConsolidatedProductRules()
    {
        await using var db = CreateDbContext();
        await SeedProductWithInventoryAsync(db, "DISP", "Produto Disponível", 100m, 25m, 75m);
        var stockout = await SeedProductWithInventoryAsync(db, "RUP", "Produto Ruptura", 20m, 30m, -10m);
        await SeedDailyInventoryAsync(db, stockout.Id, new DateOnly(2026, 7, 17), 40m, 15m);
        var service = new BusinessChatQueryService(db);
        var summaryTool = new GetInventorySummaryChatTool(service, NullLogger<GetInventorySummaryChatTool>.Instance);
        var stockoutTool = new ListStockoutProductsChatTool(
            service,
            Options.Create(new AssistantOptions()),
            NullLogger<ListStockoutProductsChatTool>.Instance);

        var summary = Serialize((await summaryTool.ExecuteAsync("{}", Context(), default)).Payload).RootElement;
        var stockouts = Serialize((await stockoutTool.ExecuteAsync("""{"limit":10}""", Context(), default)).Payload).RootElement;

        Assert.Equal(1, summary.GetProperty("stockoutProducts").GetInt32());
        Assert.Equal(120m, summary.GetProperty("totalOnHandQuantity").GetDecimal());
        Assert.Equal(55m, summary.GetProperty("totalCommittedQuantity").GetDecimal());
        Assert.Equal(65m, summary.GetProperty("totalAvailableQuantity").GetDecimal());
        Assert.Equal(650m, summary.GetProperty("totalStockValue").GetDecimal());
        Assert.Equal(550m, summary.GetProperty("totalCommittedValue").GetDecimal());
        Assert.Equal(45.83m, summary.GetProperty("committedPercent").GetDecimal());
        Assert.Equal(40m, summary.GetProperty("lastProduction").GetDecimal());
        Assert.Equal(25m, summary.GetProperty("operationalBalance").GetDecimal());
        Assert.Single(stockouts.EnumerateArray());
        Assert.Equal(stockout.Id, stockouts[0].GetProperty("productId").GetGuid());
        Assert.Equal(300m, stockouts[0].GetProperty("committedValue").GetDecimal());
    }

    [Fact]
    public async Task ProductionSummaryAndRecords_ReturnPublishedDailyProduction()
    {
        await using var db = CreateDbContext();
        var product = await SeedProductWithInventoryAsync(db, "PROD", "Produto Produzido", 10m, 1m, 9m);
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentMonthDate = new DateOnly(now.Year, now.Month, 15);
        await SeedDailyInventoryAsync(db, product.Id, currentMonthDate.AddDays(-1), 25m, 8m);
        await SeedDailyInventoryAsync(db, product.Id, currentMonthDate, 40m, 15m);
        var service = new BusinessChatQueryService(db);
        var summaryTool = new GetProductionSummaryChatTool(service, NullLogger<GetProductionSummaryChatTool>.Instance);
        var recordsTool = new ListProductionRecordsChatTool(
            service,
            Options.Create(new AssistantOptions()),
            NullLogger<ListProductionRecordsChatTool>.Instance);

        var summary = Serialize((await summaryTool.ExecuteAsync("{}", Context(), default)).Payload).RootElement;
        var records = Serialize((await recordsTool.ExecuteAsync(
            $$"""{"searchTerm":null,"productId":"{{product.Id}}","dateFrom":"{{currentMonthDate:yyyy-MM-dd}}","dateTo":"{{currentMonthDate:yyyy-MM-dd}}","sort":"production_desc","limit":10}""",
            Context(),
            default)).Payload).RootElement;

        Assert.Equal(40m, summary.GetProperty("lastProduction").GetDecimal());
        Assert.Equal(65m, summary.GetProperty("totalProductionMonth").GetDecimal());
        Assert.Single(records.EnumerateArray());
        Assert.Equal(40m, records[0].GetProperty("productionQuantity").GetDecimal());
    }

    private static ImportDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Guid DataSourceId, Guid CustomerId, Guid MunicipalityId)> SeedCustomerAsync(
        ImportDbContext db,
        string externalCode,
        string branchCode,
        string tradeName)
    {
        var now = DateTime.UtcNow;
        var source = await EnsureDataSourceAsync(db, CustomerImportCodes.DataSource, CustomerImportCodes.ProcessorKey);
        var import = await EnsureCurrentImportAsync(db, source, "clientes.xlsx");
        var municipality = await EnsureMunicipalityAsync(db, "Marília", "SP");
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            DataSourceId = source.Id,
            ExternalCode = externalCode,
            BranchCode = branchCode,
            IsActive = true,
            CreatedAt = now
        };
        db.Customers.Add(customer);
        db.CustomerSnapshots.Add(new CustomerSnapshot
        {
            Id = Guid.NewGuid(),
            ImportId = import.Id,
            CustomerId = customer.Id,
            MunicipalityId = municipality.Id,
            DocumentNumber = "12345678000199",
            DocumentType = "CNPJ",
            LegalName = tradeName,
            TradeName = tradeName,
            CustomerType = "Mercado",
            SourceRowNumber = 1,
            CreatedAt = now
        });
        await db.SaveChangesAsync();
        return (source.Id, customer.Id, municipality.Id);
    }

    private static async Task<Product> SeedProductWithInventoryAsync(
        ImportDbContext db,
        string erpCode,
        string name,
        decimal onHandQuantity,
        decimal committedQuantity,
        decimal availableQuantity)
    {
        var now = DateTime.UtcNow;
        var productSource = await EnsureDataSourceAsync(db, ProductImportCodes.DataSource, ProductImportCodes.ProcessorKey);
        var inventorySource = await EnsureDataSourceAsync(db, InventoryCurrentImportCodes.DataSource, InventoryCurrentImportCodes.ProcessorKey);
        var inventoryImport = await EnsureCurrentImportAsync(db, inventorySource, "estoque.xlsx");
        var product = new Product
        {
            Id = Guid.NewGuid(),
            DataSourceId = productSource.Id,
            ExternalCode = erpCode,
            ErpCode = erpCode,
            OperationalCode = $"OP-{erpCode}",
            Description = $"{name} - descrição comercial",
            Name = name,
            Type = "PA",
            Unit = "UN",
            GroupCode = "G1",
            NetWeightKg = 1,
            GrossWeightKg = 1.2m,
            Gtin = "7891234567890",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Products.Add(product);
        db.InventorySnapshots.Add(new InventorySnapshot
        {
            Id = Guid.NewGuid(),
            ImportId = inventoryImport.Id,
            ProductId = product.Id,
            BranchCode = "01",
            WarehouseCode = "01",
            OnHandQuantity = onHandQuantity,
            CommittedQuantity = committedQuantity,
            AvailableQuantity = availableQuantity,
            StockValue = availableQuantity * 10,
            CommittedValue = committedQuantity * 10,
            SourceRowNumber = 1,
            CreatedAt = now
        });
        await db.SaveChangesAsync();
        return product;
    }

    private static async Task SeedDailyInventoryAsync(
        ImportDbContext db,
        Guid productId,
        DateOnly date,
        decimal production,
        decimal outbound)
    {
        var source = await EnsureDataSourceAsync(db, DailyInventoryImportCodes.DataSource, DailyInventoryImportCodes.ProcessorKey);
        var import = await EnsureCurrentImportAsync(db, source, "diario.xlsx");
        db.DailyInventoryRecords.Add(new DailyInventoryRecord
        {
            Id = Guid.NewGuid(),
            ImportId = import.Id,
            ProductId = productId,
            Date = date,
            ProductionQuantity = production,
            OutboundQuantity = outbound,
            ClosingQuantity = production - outbound,
            AdjustmentQuantity = 0,
            SourceSheetName = "Julho",
            SourceRowNumber = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedFiscalDocumentAsync(
        ImportDbContext db,
        Guid dataSourceId,
        Guid customerId,
        Guid municipalityId,
        string documentNumber,
        DateOnly issueDate,
        FiscalMovementCategory category,
        decimal grossWeightKg,
        decimal? unitValue,
        Guid? productId = null,
        decimal quantity = 1,
        decimal? sourceTotalValue = null)
    {
        var now = DateTime.UtcNow;
        var import = await EnsureCurrentImportAsync(
            db,
            await db.DataSources.SingleAsync(source => source.Id == dataSourceId),
            "fiscal.xlsx");
        var document = new FiscalDocument
        {
            Id = Guid.NewGuid(),
            DataSourceId = dataSourceId,
            DocumentNumber = documentNumber,
            Series = "1",
            DocumentType = "NF",
            MovementType = category.ToString(),
            IssueDate = issueDate,
            CustomerId = customerId,
            MunicipalityId = municipalityId,
            CustomerCodeAtIssue = "C001",
            BranchCodeAtIssue = "01",
            CustomerNameAtIssue = "Cliente Teste",
            CityNameAtIssue = "Marília",
            StateCodeAtIssue = "SP",
            OperationCode = category.ToString(),
            OperationDescription = category.ToString(),
            MovementCategory = category,
            FirstSeenImportId = import.Id,
            LastSeenImportId = import.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        document.Items.Add(new FiscalDocumentItem
        {
            Id = Guid.NewGuid(),
            ItemNumber = "1",
            ProductId = productId,
            ProductCode = "P1",
            ProductDescription = "Produto",
            ProductGroupCode = "G1",
            ProductGroupDescription = "Grupo",
            Quantity = quantity,
            GrossWeightKg = grossWeightKg,
            UnitValue = unitValue,
            SourceTotalValue = sourceTotalValue,
            Expenses = 1.5m,
            Ipi = 2m,
            Icms = 3m,
            Iss = 0.5m,
            CfopCode = "5102",
            CfopDescription = "Venda",
            TesCode = "501",
            TesDescription = "Venda padrão",
            OrderNumber = "PED-1",
            WarehouseCode = "01",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.FiscalDocuments.Add(document);
        await db.SaveChangesAsync();
    }

    private static async Task<DataSource> EnsureDataSourceAsync(
        ImportDbContext db,
        string code,
        string processorKey)
    {
        var source = await db.DataSources.SingleOrDefaultAsync(item => item.Code == code);
        if (source is not null)
        {
            return source;
        }

        source = new DataSource
        {
            Id = Guid.NewGuid(),
            Code = code,
            ProcessorKey = processorKey,
            Name = code,
            Type = "XLSX",
            ImportMode = DataSourceImportMode.Snapshot,
            NextImportVersion = 2,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();
        return source;
    }

    private static async Task<RouteImport> EnsureCurrentImportAsync(
        ImportDbContext db,
        DataSource source,
        string fileName)
    {
        if (source.CurrentImportId.HasValue)
        {
            return await db.RouteImports.SingleAsync(item => item.Id == source.CurrentImportId.Value);
        }

        var import = new RouteImport
        {
            Id = Guid.NewGuid(),
            DataSourceId = source.Id,
            Version = 1,
            FileName = fileName,
            FilePath = fileName,
            Status = RouteImportStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            FinishedAt = DateTime.UtcNow
        };
        source.CurrentImportId = import.Id;
        db.RouteImports.Add(import);
        await db.SaveChangesAsync();
        return import;
    }

    private static async Task<Municipality> EnsureMunicipalityAsync(ImportDbContext db, string name, string state)
    {
        var normalizedName = MunicipalityNameNormalizer.Normalize(name);
        var municipality = await db.Municipalities.SingleOrDefaultAsync(item =>
            item.StateCode == state &&
            item.NormalizedName == normalizedName);
        if (municipality is not null)
        {
            return municipality;
        }

        municipality = new Municipality
        {
            Id = Guid.NewGuid(),
            Name = name,
            StateCode = state,
            NormalizedName = normalizedName
        };
        db.Municipalities.Add(municipality);
        await db.SaveChangesAsync();
        return municipality;
    }

    private static ChatExecutionContext Context() => new(1, "logistica");

    private static JsonDocument Serialize(object payload) =>
        JsonDocument.Parse(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}
