using System.Diagnostics;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Assistant;

public sealed class SearchRoutesChatTool(
    IRouteChatQueryService routeQueries,
    IOptions<AssistantOptions> options,
    ILogger<SearchRoutesChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 10;
    private const int MinimumSearchTermLength = 2;
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "search_routes";
    public string Description => "Pesquisa rotas pelo nome da rota ou pelo nome de uma cidade atendida.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            searchTerm = new { type = "string", description = "Nome, trecho de nome ou cidade da rota." },
            limit = new { type = "integer", minimum = 1, maximum = assistantOptions.MaximumGeneralSearchResults }
        },
        required = new[] { "searchTerm", "limit" }
    };

    public async Task<ChatToolResult> ExecuteAsync(
        string argumentsJson,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var searchTerm = ReadString(document.RootElement, "searchTerm");
            if (searchTerm.Length < MinimumSearchTermLength)
            {
                return ChatToolResult.Fail("Informe ao menos 2 caracteres para pesquisar rotas.");
            }

            var limit = ReadLimit(document.RootElement, DefaultLimit, assistantOptions.MaximumGeneralSearchResults);
            var routes = await routeQueries.SearchRoutesAsync(searchTerm, limit, cancellationToken);
            LogSuccess(context, routes.Count, startedAt);
            return ChatToolResult.Ok(routes, routes.Count);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para pesquisa de rotas.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível pesquisar rotas agora.");
        }
    }

    private void LogSuccess(ChatExecutionContext context, int recordCount, long startedAt) =>
        logger.LogInformation(
            "Chat tool {ToolName} executada para usuário {UserId} em {ElapsedMs} ms com {RecordCount} registros.",
            Name,
            context.UserId,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            recordCount);

    internal static string ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    internal static int ReadLimit(JsonElement root, int defaultLimit, int maximumLimit)
    {
        var limit = root.TryGetProperty("limit", out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : defaultLimit;
        return Math.Clamp(limit, 1, maximumLimit);
    }
}

public sealed class GetRouteDetailsChatTool(
    IRouteChatQueryService routeQueries,
    ILogger<GetRouteDetailsChatTool> logger) : IChatTool
{
    public string Name => "get_route_details";
    public string Description => "Consulta o resumo seguro de uma rota específica pelo identificador.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            routeId = new { type = "string", format = "uuid", description = "Identificador da rota." }
        },
        required = new[] { "routeId" }
    };

    public async Task<ChatToolResult> ExecuteAsync(
        string argumentsJson,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var routeIdValue = SearchRoutesChatTool.ReadString(document.RootElement, "routeId");
            if (!Guid.TryParse(routeIdValue, out var routeId))
            {
                return ChatToolResult.Fail("Identificador de rota inválido.");
            }

            var route = await routeQueries.GetRouteDetailsAsync(routeId, cancellationToken);
            object payload = route is null
                ? new { found = false, message = "Rota não encontrada." }
                : new { found = true, route };
            var recordCount = route is null ? 0 : 1;
            logger.LogInformation(
                "Chat tool {ToolName} executada para usuário {UserId} em {ElapsedMs} ms com {RecordCount} registros.",
                Name,
                context.UserId,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                recordCount);
            return ChatToolResult.Ok(payload, recordCount);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para consulta de rota.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível consultar a rota agora.");
        }
    }
}

public sealed class GetCriticalRoutesChatTool(
    IRouteChatQueryService routeQueries,
    IOptions<AssistantOptions> options,
    ILogger<GetCriticalRoutesChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 10;
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "get_critical_routes";
    public string Description => "Lista rotas críticas, classificadas pela regra de ocupação logística já existente.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            limit = new { type = "integer", minimum = 1, maximum = assistantOptions.MaximumGeneralSearchResults }
        },
        required = new[] { "limit" }
    };

    public async Task<ChatToolResult> ExecuteAsync(
        string argumentsJson,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var limit = SearchRoutesChatTool.ReadLimit(document.RootElement, DefaultLimit, assistantOptions.MaximumGeneralSearchResults);
            var routes = await routeQueries.GetCriticalRoutesAsync(limit, cancellationToken);
            logger.LogInformation(
                "Chat tool {ToolName} executada para usuário {UserId} em {ElapsedMs} ms com {RecordCount} registros.",
                Name,
                context.UserId,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                routes.Count);
            return ChatToolResult.Ok(routes, routes.Count);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para listagem de rotas críticas.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível listar rotas críticas agora.");
        }
    }
}

public sealed class ListRoutesByOccupancyChatTool(
    IRouteChatQueryService routeQueries,
    IOptions<AssistantOptions> options,
    ILogger<ListRoutesByOccupancyChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 10;
    private static readonly HashSet<string> SupportedOccupancyLevels =
    [
        "critical",
        "good",
        "medium",
        "idle",
        "unavailable"
    ];
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "list_routes_by_occupancy";

    public string Description =>
        "Lista rotas por classificação ou faixa de ocupação. Use para perguntas sobre rotas críticas, ociosas, saudáveis, médias, sem ocupação, maiores ou menores ocupações.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            occupancyLevel = new
            {
                type = new[] { "string", "null" },
                @enum = new object?[] { "critical", "good", "medium", "idle", "unavailable", null },
                description = "Classificação desejada. Use idle para rotas ociosas e critical para rotas críticas."
            },
            minimumOccupancyPercentage = new
            {
                type = new[] { "number", "null" },
                minimum = 0,
                maximum = 300,
                description = "Percentual mínimo de ocupação quando a pergunta mencionar uma faixa."
            },
            maximumOccupancyPercentage = new
            {
                type = new[] { "number", "null" },
                minimum = 0,
                maximum = 300,
                description = "Percentual máximo de ocupação quando a pergunta mencionar uma faixa."
            },
            sortDirection = new
            {
                type = "string",
                @enum = new[] { "asc", "desc" },
                description = "Use asc para menores/mais ociosas e desc para maiores/mais carregadas."
            },
            limit = new { type = "integer", minimum = 1, maximum = assistantOptions.MaximumGeneralSearchResults }
        },
        required = new[]
        {
            "occupancyLevel",
            "minimumOccupancyPercentage",
            "maximumOccupancyPercentage",
            "sortDirection",
            "limit"
        }
    };

    public async Task<ChatToolResult> ExecuteAsync(
        string argumentsJson,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var occupancyLevel = ReadNullableString(document.RootElement, "occupancyLevel")?.ToLowerInvariant();
            if (occupancyLevel is not null && !SupportedOccupancyLevels.Contains(occupancyLevel))
            {
                return ChatToolResult.Fail("Classificação de ocupação inválida.");
            }

            var sortDirection = SearchRoutesChatTool.ReadString(document.RootElement, "sortDirection").ToLowerInvariant();
            if (sortDirection is not "asc" and not "desc")
            {
                sortDirection = occupancyLevel == "idle" ? "asc" : "desc";
            }

            var query = new RouteChatOccupancyQuery(
                occupancyLevel,
                ReadNullableDecimal(document.RootElement, "minimumOccupancyPercentage"),
                ReadNullableDecimal(document.RootElement, "maximumOccupancyPercentage"),
                sortDirection,
                SearchRoutesChatTool.ReadLimit(
                    document.RootElement,
                    DefaultLimit,
                    assistantOptions.MaximumGeneralSearchResults));

            var routes = await routeQueries.ListRoutesByOccupancyAsync(query, cancellationToken);
            logger.LogInformation(
                "Chat tool {ToolName} executada para usuário {UserId} em {ElapsedMs} ms com {RecordCount} registros.",
                Name,
                context.UserId,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                routes.Count);
            return ChatToolResult.Ok(routes, routes.Count);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para listagem de rotas por ocupação.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível listar rotas por ocupação agora.");
        }
    }

    private static string? ReadNullableString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }

    private static decimal? ReadNullableDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.TryGetDecimal(out var parsed)
            ? Math.Clamp(parsed, 0m, 300m)
            : null;
    }
}

public sealed class GetRouteCitiesChatTool(
    IRouteChatQueryService routeQueries,
    IOptions<AssistantOptions> options,
    ILogger<GetRouteCitiesChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 30;
    private const int MaximumCityLimit = 30;
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "get_route_cities";
    public string Description => "Lista as cidades vinculadas a uma rota específica.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            routeId = new { type = "string", format = "uuid", description = "Identificador da rota." },
            limit = new { type = "integer", minimum = 1, maximum = MaximumCityLimit }
        },
        required = new[] { "routeId", "limit" }
    };

    public async Task<ChatToolResult> ExecuteAsync(
        string argumentsJson,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var routeIdValue = SearchRoutesChatTool.ReadString(document.RootElement, "routeId");
            if (!Guid.TryParse(routeIdValue, out var routeId))
            {
                return ChatToolResult.Fail("Identificador de rota inválido.");
            }

            var limit = SearchRoutesChatTool.ReadLimit(
                document.RootElement,
                DefaultLimit,
                Math.Min(MaximumCityLimit, assistantOptions.MaximumGeneralSearchResults));
            var route = await routeQueries.GetRouteCitiesAsync(routeId, limit, cancellationToken);
            object payload = route is null
                ? new { found = false, message = "Rota não encontrada." }
                : new { found = true, route };
            var recordCount = route?.Cities.Count ?? 0;
            logger.LogInformation(
                "Chat tool {ToolName} executada para usuário {UserId} em {ElapsedMs} ms com {RecordCount} registros.",
                Name,
                context.UserId,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                recordCount);
            return ChatToolResult.Ok(payload, recordCount);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para listagem de cidades da rota.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível listar cidades da rota agora.");
        }
    }
}

public sealed class GetRouteCustomersChatTool(
    IRouteChatQueryService routeQueries,
    IOptions<AssistantOptions> options,
    ILogger<GetRouteCustomersChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 20;
    private const int MaximumCustomerLimit = 50;
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "get_route_customers";

    public string Description =>
        "Lista clientes vinculados a uma rota. Nesta versão, o vínculo é inferido pelos municípios dos clientes que aparecem nas cidades da rota.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            routeId = new { type = "string", format = "uuid", description = "Identificador da rota." },
            limit = new { type = "integer", minimum = 1, maximum = MaximumCustomerLimit }
        },
        required = new[] { "routeId", "limit" }
    };

    public async Task<ChatToolResult> ExecuteAsync(
        string argumentsJson,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var routeIdValue = SearchRoutesChatTool.ReadString(document.RootElement, "routeId");
            if (!Guid.TryParse(routeIdValue, out var routeId))
            {
                return ChatToolResult.Fail("Identificador de rota inválido.");
            }

            var limit = SearchRoutesChatTool.ReadLimit(
                document.RootElement,
                DefaultLimit,
                Math.Min(MaximumCustomerLimit, assistantOptions.MaximumGeneralSearchResults));
            var route = await routeQueries.GetRouteCustomersAsync(routeId, limit, cancellationToken);
            object payload = route is null
                ? new { found = false, message = "Rota não encontrada." }
                : new { found = true, route };
            var recordCount = route?.Customers.Count ?? 0;
            logger.LogInformation(
                "Chat tool {ToolName} executada para usuário {UserId} em {ElapsedMs} ms com {RecordCount} registros.",
                Name,
                context.UserId,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                recordCount);
            return ChatToolResult.Ok(payload, recordCount);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para listagem de clientes da rota.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível listar clientes da rota agora.");
        }
    }
}
