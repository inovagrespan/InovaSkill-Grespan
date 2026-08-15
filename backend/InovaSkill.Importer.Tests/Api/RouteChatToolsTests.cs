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

public sealed class RouteChatToolsTests
{

    [Fact]
    public async Task SearchRoutes_WithValidTerm_ReturnsMatchingRoute()
    {
        await using var db = CreateDbContext();
        var route = await SeedRouteAsync(db, "Rota Marília", 0.974m);
        var tool = new SearchRoutesChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<SearchRoutesChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"searchTerm":"Marília","limit":10}""",
            Context(),
            default);
        var json = Serialize(result.Payload);

        Assert.True(result.Success);
        Assert.Equal(route.Id, json.RootElement[0].GetProperty("id").GetGuid());
        Assert.Equal("Crítico", json.RootElement[0].GetProperty("status").GetString());
        Assert.Equal(97.4m, json.RootElement[0].GetProperty("occupancyPercentage").GetDecimal());
        Assert.Equal("MONDAY", json.RootElement[0].GetProperty("weekday").GetString());
        Assert.Equal(900m, json.RootElement[0].GetProperty("totalWeightKg").GetDecimal());
        Assert.Equal(90m, json.RootElement[0].GetProperty("weightOccupancyPercentage").GetDecimal());
        Assert.Equal(75m, json.RootElement[0].GetProperty("volumeOccupancyPercentage").GetDecimal());
    }

    [Fact]
    public async Task SearchRoutes_WithShortTerm_ReturnsControlledError()
    {
        await using var db = CreateDbContext();
        var tool = new SearchRoutesChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<SearchRoutesChatTool>.Instance);

        var result = await tool.ExecuteAsync("""{"searchTerm":"A","limit":10}""", Context(), default);

        Assert.False(result.Success);
        Assert.Contains("2 caracteres", result.ErrorMessage);
    }

    [Fact]
    public async Task SearchRoutes_ClampsLimitToConfiguredMaximum()
    {
        await using var db = CreateDbContext();
        await SeedRouteAsync(db, "Rota 1", 0.90m);
        await SeedRouteAsync(db, "Rota 2", 0.91m);
        await SeedRouteAsync(db, "Rota 3", 0.92m);
        var tool = new SearchRoutesChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions { MaximumGeneralSearchResults = 2 }),
            NullLogger<SearchRoutesChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"searchTerm":"Rota","limit":99}""",
            Context(),
            default);

        Assert.Equal(2, result.RecordCount);
    }

    [Fact]
    public async Task GetRouteDetails_ReturnsExistingRoute()
    {
        await using var db = CreateDbContext();
        var route = await SeedRouteAsync(db, "Rota Detalhe", 1.12m);
        var tool = new GetRouteDetailsChatTool(
            new RouteChatQueryService(db),
            NullLogger<GetRouteDetailsChatTool>.Instance);

        var result = await tool.ExecuteAsync($$"""{"routeId":"{{route.Id}}"}""", Context(), default);
        var json = Serialize(result.Payload);

        Assert.True(json.RootElement.GetProperty("found").GetBoolean());
        var routeJson = json.RootElement.GetProperty("route");
        Assert.Equal("Crítico", routeJson.GetProperty("status").GetString());
        Assert.Equal(112m, routeJson.GetProperty("occupancyPercentage").GetDecimal());
        Assert.Equal(1000m, routeJson.GetProperty("vehicleCapacityKg").GetDecimal());
        Assert.Equal(12m, routeJson.GetProperty("vehicleCapacityVolumeM3").GetDecimal());
        Assert.Equal(10, routeJson.GetProperty("vehicleCapacityPallets").GetInt32());
        Assert.Equal(1, routeJson.GetProperty("cityCount").GetInt32());
        Assert.Equal(3, routeJson.GetProperty("deliveryCount").GetInt32());
        Assert.Equal(0, routeJson.GetProperty("potentialCustomerCount").GetInt32());
    }

    [Fact]
    public async Task GetRouteDetails_ReturnsNotFoundPayload()
    {
        await using var db = CreateDbContext();
        var tool = new GetRouteDetailsChatTool(
            new RouteChatQueryService(db),
            NullLogger<GetRouteDetailsChatTool>.Instance);

        var result = await tool.ExecuteAsync($$"""{"routeId":"{{Guid.NewGuid()}}"}""", Context(), default);
        var json = Serialize(result.Payload);

        Assert.False(json.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal("Rota não encontrada.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetCriticalRoutes_UsesExistingOccupancyPolicy()
    {
        await using var db = CreateDbContext();
        await SeedRouteAsync(db, "Rota Crítica", 0.9501m);
        await SeedRouteAsync(db, "Rota Saudável", 0.95m);
        var tool = new GetCriticalRoutesChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<GetCriticalRoutesChatTool>.Instance);

        var result = await tool.ExecuteAsync("""{"limit":10}""", Context(), default);
        var json = Serialize(result.Payload);

        Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("Rota Crítica", json.RootElement[0].GetProperty("name").GetString());
        Assert.Equal("Ocupação acima do limite saudável.", json.RootElement[0].GetProperty("reason").GetString());
        Assert.Equal(90m, json.RootElement[0].GetProperty("weightOccupancyPercentage").GetDecimal());
    }

    [Fact]
    public async Task ListRoutesByOccupancy_ReturnsMostIdleRoutes()
    {
        await using var db = CreateDbContext();
        await SeedRouteAsync(db, "Rota Menor", 0.20m);
        await SeedRouteAsync(db, "Rota Intermediária", 0.40m);
        await SeedRouteAsync(db, "Rota Fora", 0.70m);
        var tool = new ListRoutesByOccupancyChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<ListRoutesByOccupancyChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"occupancyLevel":"idle","minimumOccupancyPercentage":null,"maximumOccupancyPercentage":null,"sortDirection":"asc","limit":2}""",
            Context(),
            default);
        var json = Serialize(result.Payload);

        Assert.Equal(2, result.RecordCount);
        Assert.Equal("Rota Menor", json.RootElement[0].GetProperty("name").GetString());
        Assert.Equal(20.0m, json.RootElement[0].GetProperty("occupancyPercentage").GetDecimal());
        Assert.Equal("Rota Intermediária", json.RootElement[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListRoutesByOccupancy_AppliesPercentageRange()
    {
        await using var db = CreateDbContext();
        await SeedRouteAsync(db, "Rota 130", 1.30m);
        await SeedRouteAsync(db, "Rota 150", 1.50m);
        await SeedRouteAsync(db, "Rota 210", 2.10m);
        var tool = new ListRoutesByOccupancyChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<ListRoutesByOccupancyChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"occupancyLevel":null,"minimumOccupancyPercentage":140,"maximumOccupancyPercentage":200,"sortDirection":"desc","limit":10}""",
            Context(),
            default);
        var json = Serialize(result.Payload);

        Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("Rota 150", json.RootElement[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetRouteCities_ReturnsLimitedCities()
    {
        await using var db = CreateDbContext();
        var route = await SeedRouteAsync(db, "Rota Cidades", 0.90m, extraCity: "Bauru");
        var tool = new GetRouteCitiesChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<GetRouteCitiesChatTool>.Instance);

        var result = await tool.ExecuteAsync($$"""{"routeId":"{{route.Id}}","limit":1}""", Context(), default);
        var json = Serialize(result.Payload);

        Assert.True(json.RootElement.GetProperty("found").GetBoolean());
        var cities = json.RootElement.GetProperty("route").GetProperty("cities");
        Assert.Single(cities.EnumerateArray());
        Assert.Equal("Marília", cities[0].GetProperty("name").GetString());
        Assert.Equal("SP", cities[0].GetProperty("state").GetString());
        Assert.Equal(1, cities[0].GetProperty("sequence").GetInt32());
        Assert.Equal(3, cities[0].GetProperty("deliveries").GetInt32());
        Assert.Equal(12m, cities[0].GetProperty("averagePerDay").GetDecimal());
        Assert.Equal("Prioridade comercial", cities[0].GetProperty("note").GetString());
    }

    [Fact]
    public async Task GetRouteCustomers_ReturnsActiveCustomersFromRouteMunicipalities()
    {
        await using var db = CreateDbContext();
        var route = await SeedRouteAsync(db, "Rota Clientes", 0.90m);
        var currentCustomerImportId = await SeedCustomerSourceAsync(db);
        var marilia = await db.Municipalities.SingleAsync(item => item.Name == "Marília");
        var bauru = new Municipality
        {
            Id = Guid.NewGuid(),
            StateCode = "SP",
            Name = "Bauru",
            NormalizedName = MunicipalityNameNormalizer.Normalize("Bauru")
        };
        db.Municipalities.Add(bauru);
        AddCustomerSnapshot(db, currentCustomerImportId, marilia.Id, "0001", "01", "Padaria Marília", true);
        AddCustomerSnapshot(db, currentCustomerImportId, marilia.Id, "0002", "01", "Mercado Marília", true);
        AddCustomerSnapshot(db, currentCustomerImportId, bauru.Id, "0003", "01", "Mercado Bauru", true);
        AddCustomerSnapshot(db, currentCustomerImportId, marilia.Id, "0004", "01", "Inativo Marília", false);
        await db.SaveChangesAsync();
        await new RouteCustomerAssignmentSynchronizer(db).SyncInferredAssignmentsAsync(default);
        var tool = new GetRouteCustomersChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<GetRouteCustomersChatTool>.Instance);

        var result = await tool.ExecuteAsync($$"""{"routeId":"{{route.Id}}","limit":10}""", Context(), default);
        var json = Serialize(result.Payload);

        Assert.True(json.RootElement.GetProperty("found").GetBoolean());
        var routeJson = json.RootElement.GetProperty("route");
        Assert.Equal("InferredByMunicipality", routeJson.GetProperty("relationshipType").GetString());
        var customers = routeJson.GetProperty("customers");
        Assert.Equal(2, customers.GetArrayLength());
        Assert.Equal(2, await db.RouteCustomerAssignments.CountAsync());
        Assert.Equal("0001", customers[0].GetProperty("code").GetString());
        Assert.Equal("Padaria Marília", customers[0].GetProperty("tradeName").GetString());
        Assert.All(customers.EnumerateArray(), customer =>
            Assert.Equal("Marília", customer.GetProperty("municipalityName").GetString()));
    }


    [Fact]
    public async Task GetRouteDetails_CountsPotentialCustomersByRouteMunicipality()
    {
        await using var db = CreateDbContext();
        var route = await SeedRouteAsync(db, "Rota Contagem", 0.90m);
        var currentCustomerImportId = await SeedCustomerSourceAsync(db);
        var marilia = await db.Municipalities.SingleAsync(item => item.Name == "Marília");
        AddCustomerSnapshot(db, currentCustomerImportId, marilia.Id, "0001", "01", "Padaria Marília", true);
        AddCustomerSnapshot(db, currentCustomerImportId, marilia.Id, "0002", "01", "Mercado Marília", true);
        AddCustomerSnapshot(db, currentCustomerImportId, marilia.Id, "0003", "01", "Inativo Marília", false);
        await db.SaveChangesAsync();
        await new RouteCustomerAssignmentSynchronizer(db).SyncInferredAssignmentsAsync(default);
        var tool = new GetRouteDetailsChatTool(
            new RouteChatQueryService(db),
            NullLogger<GetRouteDetailsChatTool>.Instance);

        var result = await tool.ExecuteAsync($$"""{"routeId":"{{route.Id}}"}""", Context(), default);
        var routeJson = Serialize(result.Payload).RootElement.GetProperty("route");

        Assert.Equal(2, routeJson.GetProperty("potentialCustomerCount").GetInt32());
    }

    private static ImportDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Route> SeedRouteAsync(
        ImportDbContext db,
        string routeName,
        decimal? occupancy,
        string? extraCity = null)
    {
        var now = DateTime.UtcNow;
        var source = await db.DataSources.SingleOrDefaultAsync(source => source.Code == RouteImportCodes.DataSource);
        if (source is null)
        {
            source = new DataSource
            {
                Id = Guid.NewGuid(),
                Code = RouteImportCodes.DataSource,
                ProcessorKey = RouteImportCodes.ProcessorKey,
                Name = "Rotas",
                Type = "XLSX",
                ImportMode = DataSourceImportMode.Snapshot,
                NextImportVersion = 2,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            var import = new RouteImport
            {
                Id = Guid.NewGuid(),
                DataSourceId = source.Id,
                Version = 1,
                FileName = "rotas.xlsx",
                FilePath = "rotas.xlsx",
                Status = RouteImportStatus.Completed,
                CreatedAt = now,
                FinishedAt = now
            };
            source.CurrentImportId = import.Id;
            db.AddRange(source, import);
        }

        var vehicle = new VehicleType
        {
            Id = Guid.NewGuid(),
            Name = $"Truck {Guid.NewGuid()}",
            CapacityKg = 1000m,
            CapacityVolumeM3 = 12m,
            CapacityPallets = 10
        };
        var route = new Route
        {
            Id = Guid.NewGuid(),
            ImportId = source.CurrentImportId!.Value,
            Name = routeName,
            Weekday = "MONDAY",
            VehicleTypeId = vehicle.Id,
            TotalWeightKg = 900m,
            TotalVolumeM3 = 9m,
            TotalPallets = 8,
            WeightOccupancy = 0.90m,
            VolumeOccupancy = 0.75m,
            PalletOccupancy = 0.80m,
            OverallOccupancy = occupancy,
            OccupancyStatus = occupancy.HasValue ? RouteOccupancyStatus.Calculated : RouteOccupancyStatus.MissingCapacity,
            CreatedAt = now
        };
        var municipality = new Municipality
        {
            Id = Guid.NewGuid(),
            StateCode = "SP",
            Name = "Marília",
            NormalizedName = MunicipalityNameNormalizer.Normalize("Marília")
        };
        route.Entries.Add(new RouteEntry
        {
            Id = Guid.NewGuid(),
            Sequence = 1,
            Name = "MARILIA",
            Municipality = municipality,
            Deliveries = 3,
            AveragePerDay = 12m,
            Note = "Prioridade comercial",
            CreatedAt = now
        });
        if (extraCity is not null)
        {
            route.Entries.Add(new RouteEntry
            {
                Id = Guid.NewGuid(),
                Sequence = 2,
                Name = extraCity.ToUpperInvariant(),
                Deliveries = 1,
                AveragePerDay = 4m,
                CreatedAt = now
            });
        }

        db.AddRange(vehicle, route);
        await db.SaveChangesAsync();
        return route;
    }

    private static ChatExecutionContext Context() => new(1, "logistica");

    private static JsonDocument Serialize(object payload) =>
        JsonDocument.Parse(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));


    private static async Task<Guid> SeedCustomerSourceAsync(ImportDbContext db)
    {
        var now = DateTime.UtcNow;
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Code = CustomerImportCodes.DataSource,
            ProcessorKey = CustomerImportCodes.ProcessorKey,
            Name = "Clientes",
            Type = "XLSX",
            ImportMode = DataSourceImportMode.Snapshot,
            NextImportVersion = 2,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var import = new RouteImport
        {
            Id = Guid.NewGuid(),
            DataSourceId = source.Id,
            Version = 1,
            FileName = "clientes.xlsx",
            FilePath = "clientes.xlsx",
            Status = RouteImportStatus.Completed,
            CreatedAt = now,
            FinishedAt = now
        };
        source.CurrentImportId = import.Id;
        db.AddRange(source, import);
        await db.SaveChangesAsync();
        return import.Id;
    }

    private static void AddCustomerSnapshot(
        ImportDbContext db,
        Guid importId,
        Guid municipalityId,
        string externalCode,
        string branchCode,
        string tradeName,
        bool active)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            DataSourceId = db.DataSources.Single(source => source.Code == CustomerImportCodes.DataSource).Id,
            ExternalCode = externalCode,
            BranchCode = branchCode,
            IsActive = active,
            CreatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        db.CustomerSnapshots.Add(new CustomerSnapshot
        {
            Id = Guid.NewGuid(),
            ImportId = importId,
            CustomerId = customer.Id,
            MunicipalityId = municipalityId,
            DocumentNumber = $"DOC-{externalCode}",
            DocumentType = "CNPJ",
            LegalName = tradeName,
            TradeName = tradeName,
            CustomerType = "Mercado",
            SourceRowNumber = 1,
            CreatedAt = DateTime.UtcNow
        });
    }
}
