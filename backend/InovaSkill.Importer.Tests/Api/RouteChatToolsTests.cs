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
        Assert.Equal(3, await db.RouteCustomerAssignments.CountAsync());
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

    [Fact]
    public async Task GetDailyRouteOptimization_ReturnsOnlyPersistedCanonicalResult()
    {
        await using var db = CreateDbContext();
        var route = await SeedRouteAsync(db, "Marília 1", 1.12m);
        await SeedOptimizationAsync(db, route);
        var tool = new GetDailyRouteOptimizationChatTool(
            new RouteChatQueryService(db),
            NullLogger<GetDailyRouteOptimizationChatTool>.Instance);

        var result = await tool.ExecuteAsync("""{"weekday":"MONDAY"}""", Context(), default);
        var payload = Serialize(result.Payload).RootElement;

        Assert.True(result.Success);
        Assert.True(payload.GetProperty("found").GetBoolean());
        var optimization = payload.GetProperty("optimization");
        Assert.Equal("Optimized", optimization.GetProperty("status").GetString());
        Assert.Contains("não há correspondência 1:1", optimization.GetProperty("comparisonScope").GetString());
        Assert.Equal(100m, optimization.GetProperty("current").GetProperty("distanceKm").GetDecimal());
        Assert.Equal(80m, optimization.GetProperty("proposed").GetProperty("distanceKm").GetDecimal());
        Assert.Equal("Marília", optimization.GetProperty("proposedVehicles")[0]
            .GetProperty("stops")[0].GetProperty("municipality").GetString());
    }

    [Fact]
    public async Task GetDailyRouteOptimization_WithoutSnapshot_ReturnsDataInsufficient()
    {
        await using var db = CreateDbContext();
        await SeedRouteAsync(db, "Marília 1", 1.12m);
        var tool = new GetDailyRouteOptimizationChatTool(
            new RouteChatQueryService(db),
            NullLogger<GetDailyRouteOptimizationChatTool>.Instance);

        var result = await tool.ExecuteAsync("""{"weekday":"MONDAY"}""", Context(), default);
        var payload = Serialize(result.Payload).RootElement;

        Assert.False(payload.GetProperty("found").GetBoolean());
        Assert.Contains("Dados insuficientes", payload.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetRouteOperationalAnalysis_StatesDailyScopeAndDoesNotClaimOneToOneReplacement()
    {
        await using var db = CreateDbContext();
        var route = await SeedRouteAsync(db, "Marília 1", 1.12m);
        await SeedOptimizationAsync(db, route);
        var snapshot = await SeedRouteCostAsync(db, route, 225.45m);
        db.RouteCostItems.Add(CreateRouteCostItem(snapshot.Id, route, 180m, RouteCostScenarios.Optimized));
        await db.SaveChangesAsync();
        var tool = new GetRouteOperationalAnalysisChatTool(
            new RouteChatQueryService(db),
            NullLogger<GetRouteOperationalAnalysisChatTool>.Instance);

        var result = await tool.ExecuteAsync($$"""{"routeId":"{{route.Id}}"}""", Context(), default);
        var analysis = Serialize(result.Payload).RootElement.GetProperty("analysis");

        Assert.Equal("Marília 1", analysis.GetProperty("route").GetProperty("name").GetString());
        Assert.Equal("Crítico", analysis.GetProperty("route").GetProperty("status").GetString());
        Assert.Equal(225.45m, analysis.GetProperty("cost").GetProperty("maximumTotalCost").GetDecimal());
        var dailyCosts = analysis.GetProperty("dailyCosts");
        Assert.Equal(225.45m, dailyCosts.GetProperty("actual").GetProperty("maximumTotalCost").GetDecimal());
        Assert.Equal(180m, dailyCosts.GetProperty("optimized").GetProperty("maximumTotalCost").GetDecimal());
        Assert.Equal("OPTIMIZED_MUNICIPALITIES", dailyCosts.GetProperty("optimized").GetProperty("pathBasis").GetString());
        Assert.Contains("global do dia", analysis.GetProperty("comparisonScope").GetString());
        Assert.Equal("Optimized", analysis.GetProperty("dailyOptimization").GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetRouteOperationalAnalysis_DoesNotExposeHistoricalRoute()
    {
        await using var db = CreateDbContext();
        var historicalRoute = await SeedRouteAsync(db, "Marília histórica", 1.12m);
        var source = await db.DataSources.SingleAsync(item => item.Code == RouteImportCodes.DataSource);
        var currentImport = new RouteImport
        {
            Id = Guid.NewGuid(), DataSourceId = source.Id, Version = 2, FileName = "rotas-2.xlsx",
            FilePath = "rotas-2.xlsx", Status = RouteImportStatus.Completed,
            CreatedAt = DateTime.UtcNow, FinishedAt = DateTime.UtcNow
        };
        source.CurrentImportId = currentImport.Id;
        db.RouteImports.Add(currentImport);
        await db.SaveChangesAsync();
        var tool = new GetRouteOperationalAnalysisChatTool(
            new RouteChatQueryService(db),
            NullLogger<GetRouteOperationalAnalysisChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            $$"""{"routeId":"{{historicalRoute.Id}}"}""",
            Context(),
            default);
        var payload = Serialize(result.Payload).RootElement;

        Assert.False(payload.GetProperty("found").GetBoolean());
    }

    [Fact]
    public async Task ListRouteCosts_ReturnsOfficialSnapshotAndSortsByMaximumTotalCost()
    {
        await using var db = CreateDbContext();
        var lowerCostRoute = await SeedRouteAsync(db, "Rota Econômica", 0.80m);
        var higherCostRoute = await SeedRouteAsync(db, "Rota Cara", 0.90m);
        var snapshot = await SeedRouteCostAsync(db, lowerCostRoute, 220m);
        db.RouteCostItems.Add(CreateRouteCostItem(snapshot.Id, higherCostRoute, 350m));
        db.RouteCostItems.Add(CreateRouteCostItem(
            snapshot.Id,
            higherCostRoute,
            999m,
            RouteCostScenarios.Optimized));
        await db.SaveChangesAsync();
        var tool = new ListRouteCostsChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<ListRouteCostsChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"weekday":"MONDAY","scenario":"actual","sortBy":"totalCost","sortDirection":"desc","limit":10}""",
            Context(),
            default);
        var payload = Serialize(result.Payload).RootElement;

        Assert.Equal("Available", payload.GetProperty("status").GetString());
        Assert.Equal(570m, payload.GetProperty("summary").GetProperty("maximumTotalCost").GetDecimal());
        Assert.Equal(6.15m, payload.GetProperty("routes")[0].GetProperty("dieselPricePerLiter").GetDecimal());
        Assert.Equal("Rota Cara", payload.GetProperty("routes")[0].GetProperty("routeName").GetString());
        Assert.Equal("EXACT_CUSTOMERS", payload.GetProperty("routes")[0].GetProperty("pathBasis").GetString());
        Assert.Equal(350m, payload.GetProperty("routes")[0].GetProperty("maximumTotalCost").GetDecimal());
    }

    [Fact]
    public async Task ListRouteCosts_WhenRefreshIsPending_DoesNotExposeStaleSnapshot()
    {
        await using var db = CreateDbContext();
        var route = await SeedRouteAsync(db, "Marília 1", 1.12m);
        var snapshot = await SeedRouteCostAsync(db, route, 225.45m);
        db.JobExecutions.Add(new JobExecution
        {
            Id = Guid.NewGuid(),
            JobType = OperationalJobCodes.RouteCostConsolidation,
            Queue = "default",
            ParametersJson = "{}",
            RelatedEntityId = route.ImportId,
            Status = JobExecutionStatus.Queued,
            CreatedAt = snapshot.CalculatedAt.AddMinutes(1)
        });
        await db.SaveChangesAsync();
        var tool = new ListRouteCostsChatTool(
            new RouteChatQueryService(db),
            Options.Create(new AssistantOptions()),
            NullLogger<ListRouteCostsChatTool>.Instance);

        var result = await tool.ExecuteAsync(
            """{"weekday":"MONDAY","scenario":"actual","sortBy":"totalCost","sortDirection":"desc","limit":10}""",
            Context(),
            default);
        var payload = Serialize(result.Payload).RootElement;

        Assert.Equal("Unavailable", payload.GetProperty("status").GetString());
        Assert.Contains("desatualizados", payload.GetProperty("message").GetString());
        Assert.Empty(payload.GetProperty("routes").EnumerateArray());
    }

    [Fact]
    public void LogisticsPrompt_RequiresPersistedOptimizationAndReadOnlyChat()
    {
        Assert.Contains("estritamente somente leitura", AssistantPrompts.LogisticsSystemPrompt);
        Assert.Contains("último cenário válido", AssistantPrompts.LogisticsSystemPrompt);
        Assert.Contains("Nunca apresente um veículo proposto como substituto 1:1", AssistantPrompts.LogisticsSystemPrompt);
        Assert.Contains("get_route_operational_analysis", AssistantPrompts.LogisticsSystemPrompt);
        Assert.Contains("list_route_costs", AssistantPrompts.LogisticsSystemPrompt);
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

    private static async Task SeedOptimizationAsync(ImportDbContext db, Route route)
    {
        var municipality = route.Entries.Single().Municipality!;
        var vehicleType = await db.VehicleTypes.SingleAsync(item => item.Id == route.VehicleTypeId);
        db.DailyRouteOptimizationResults.Add(new DailyRouteOptimizationResult
        {
            Id = Guid.NewGuid(),
            RouteImportId = route.ImportId,
            JobExecutionId = Guid.NewGuid(),
            Weekday = route.Weekday,
            Status = DailyRouteOptimizationStatuses.Optimized,
            CurrentDistanceMeters = 100_000m,
            CurrentDurationSeconds = 7_200m,
            ProposedDistanceMeters = 80_000m,
            ProposedDurationSeconds = 5_400m,
            CurrentVehicleCount = 2,
            ProposedVehicleCount = 1,
            TotalWeightKg = 900m,
            CreatedAt = new DateTime(2026, 9, 14, 22, 0, 0, DateTimeKind.Utc),
            Vehicles =
            [
                new DailyRouteOptimizationVehicle
                {
                    Id = Guid.NewGuid(),
                    VehicleTypeId = vehicleType.Id,
                    SourceRouteId = route.Id,
                    Sequence = 1,
                    CapacityKg = 1_000m,
                    LoadKg = 900m,
                    Occupancy = 0.9m,
                    DistanceMeters = 80_000m,
                    DurationSeconds = 5_400m,
                    Stops =
                    [
                        new DailyRouteOptimizationStop
                        {
                            Id = Guid.NewGuid(),
                            MunicipalityId = municipality.Id,
                            Sequence = 1,
                            WeightKg = 900m
                        }
                    ]
                }
            ]
        });
        await db.SaveChangesAsync();
    }

    private static async Task<RouteCostSnapshot> SeedRouteCostAsync(
        ImportDbContext db,
        Route route,
        decimal maximumTotalCost)
    {
        var snapshot = await db.RouteCostSnapshots.SingleOrDefaultAsync(item => item.RouteImportId == route.ImportId);
        if (snapshot is null)
        {
            snapshot = new RouteCostSnapshot
            {
                Id = Guid.NewGuid(),
                RouteImportId = route.ImportId,
                JobExecutionId = Guid.NewGuid(),
                InputFingerprint = "test-fingerprint",
                DieselPricePerLiter = 6.15m,
                TollCatalogVersion = "test-v1",
                CalculatedAt = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc)
            };
            db.RouteCostSnapshots.Add(snapshot);
        }
        db.RouteCostItems.Add(CreateRouteCostItem(snapshot.Id, route, maximumTotalCost));
        await db.SaveChangesAsync();
        return snapshot;
    }

    private static RouteCostItem CreateRouteCostItem(
        Guid snapshotId,
        Route route,
        decimal maximumTotalCost,
        string scenario = RouteCostScenarios.Actual) =>
        new()
        {
            Id = Guid.NewGuid(),
            SnapshotId = snapshotId,
            Scenario = scenario,
            Weekday = route.Weekday,
            RouteId = route.Id,
            VehicleTypeId = route.VehicleTypeId,
            Label = route.Name,
            PathBasis = scenario == RouteCostScenarios.Actual
                ? RouteCostPathBases.ExactCustomers
                : RouteCostPathBases.OptimizedMunicipalities,
            IsAvailable = true,
            DistanceMeters = 100_000m,
            DurationSeconds = 7_200m,
            MinimumFuelLiters = 16m,
            MaximumFuelLiters = 20m,
            MinimumFuelCost = 98.40m,
            MaximumFuelCost = 123m,
            TollCost = maximumTotalCost - 123m,
            TollPassages = 2,
            MinimumTotalCost = maximumTotalCost - 24.60m,
            MaximumTotalCost = maximumTotalCost
        };

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
