using System.Diagnostics;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Enums;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Assistant;

public sealed class SearchCustomersChatTool(
    IBusinessChatQueryService businessQueries,
    IOptions<AssistantOptions> options,
    ILogger<SearchCustomersChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 10;
    private const int MinimumSearchTermLength = 2;
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "search_customers";

    public string Description =>
        "Localiza clientes do cadastro atual por código, razão social, nome fantasia ou município. Não retorna CPF/CNPJ.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            searchTerm = new { type = "string", description = "Código, nome, nome fantasia ou município do cliente." },
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
            var searchTerm = SearchRoutesChatTool.ReadString(document.RootElement, "searchTerm");
            if (searchTerm.Length < MinimumSearchTermLength)
            {
                return ChatToolResult.Fail("Informe ao menos 2 caracteres para pesquisar clientes.");
            }

            var customers = await businessQueries.SearchCustomersAsync(
                searchTerm,
                SearchRoutesChatTool.ReadLimit(document.RootElement, DefaultLimit, assistantOptions.MaximumGeneralSearchResults),
                cancellationToken);
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, customers.Count);
            return ChatToolResult.Ok(customers, customers.Count);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para pesquisa de clientes.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível pesquisar clientes agora.");
        }
    }
}

public sealed class GetCustomerConsumptionSummaryChatTool(
    IBusinessChatQueryService businessQueries,
    ILogger<GetCustomerConsumptionSummaryChatTool> logger) : IChatTool
{
    public string Name => "get_customer_consumption_summary";

    public string Description =>
        "Consulta consumo, última compra, movimentos recentes e evolução mensal de um cliente pelo identificador.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            customerId = new { type = "string", format = "uuid", description = "Identificador do cliente." },
            referenceDate = new
            {
                type = new[] { "string", "null" },
                format = "date",
                description = "Data de referência opcional em YYYY-MM-DD. Se omitida, usa a data atual."
            }
        },
        required = new[] { "customerId", "referenceDate" }
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
            var customerIdValue = SearchRoutesChatTool.ReadString(document.RootElement, "customerId");
            if (!Guid.TryParse(customerIdValue, out var customerId))
            {
                return ChatToolResult.Fail("Identificador de cliente inválido.");
            }

            var referenceDate = ChatToolJson.ReadNullableDate(document.RootElement, "referenceDate");
            var summary = await businessQueries.GetCustomerConsumptionSummaryAsync(customerId, referenceDate, cancellationToken);
            object payload = summary is null
                ? new { found = false, message = "Cliente não encontrado." }
                : new { found = true, summary };
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, summary is null ? 0 : 1);
            return ChatToolResult.Ok(payload, summary is null ? 0 : 1);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para consulta de consumo do cliente.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível consultar consumo do cliente agora.");
        }
    }
}

public sealed class ListRecentFiscalDocumentsChatTool(
    IBusinessChatQueryService businessQueries,
    IOptions<AssistantOptions> options,
    ILogger<ListRecentFiscalDocumentsChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 10;
    private static readonly HashSet<string> SupportedOperationCategories =
        Enum.GetNames<FiscalMovementCategory>().ToHashSet(StringComparer.OrdinalIgnoreCase);
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "list_recent_fiscal_documents";

    public string Description =>
        "Lista notas fiscais recentes por cliente, número, cidade, período ou categoria de movimento, incluindo quantidade, valor unitário, valor total de origem e valor calculável dos itens para análises de preço.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            searchTerm = new { type = new[] { "string", "null" }, description = "Texto opcional para número, cliente, código ou cidade." },
            operationCategory = new { type = new[] { "string", "null" }, @enum = new object?[] { "Unknown", "Sale", "Return", "Bonus", "Loan", "Exchange", null } },
            dateFrom = new { type = new[] { "string", "null" }, format = "date" },
            dateTo = new { type = new[] { "string", "null" }, format = "date" },
            customerId = new { type = new[] { "string", "null" }, format = "uuid" },
            limit = new { type = "integer", minimum = 1, maximum = assistantOptions.MaximumGeneralSearchResults }
        },
        required = new[] { "searchTerm", "operationCategory", "dateFrom", "dateTo", "customerId", "limit" }
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
            var operationCategory = ChatToolJson.ReadNullableString(document.RootElement, "operationCategory");
            if (operationCategory is not null && !SupportedOperationCategories.Contains(operationCategory))
            {
                return ChatToolResult.Fail("Categoria de movimento fiscal inválida.");
            }

            var customerIdValue = ChatToolJson.ReadNullableString(document.RootElement, "customerId");
            if (customerIdValue is not null && !Guid.TryParse(customerIdValue, out _))
            {
                return ChatToolResult.Fail("Identificador de cliente inválido.");
            }

            var query = new BusinessChatFiscalDocumentQuery(
                ChatToolJson.ReadNullableString(document.RootElement, "searchTerm"),
                operationCategory,
                ChatToolJson.ReadNullableDate(document.RootElement, "dateFrom"),
                ChatToolJson.ReadNullableDate(document.RootElement, "dateTo"),
                customerIdValue is null ? null : Guid.Parse(customerIdValue),
                SearchRoutesChatTool.ReadLimit(document.RootElement, DefaultLimit, assistantOptions.MaximumGeneralSearchResults));

            var documents = await businessQueries.ListRecentFiscalDocumentsAsync(query, cancellationToken);
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, documents.Count);
            return ChatToolResult.Ok(documents, documents.Count);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para consulta de notas fiscais.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível consultar notas fiscais agora.");
        }
    }
}

public sealed class GetFiscalReturnRateChatTool(
    IBusinessChatQueryService businessQueries,
    ILogger<GetFiscalReturnRateChatTool> logger) : IChatTool
{
    private const int DefaultPeriodDays = 30;
    private const int MinimumPeriodDays = 1;
    private const int MaximumPeriodDays = 365;

    public string Name => "get_fiscal_return_rate";

    public string Description =>
        "Consulta a taxa fiscal de devolução por peso em um período limitado.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            periodDays = new { type = "integer", minimum = MinimumPeriodDays, maximum = MaximumPeriodDays },
            dateTo = new { type = new[] { "string", "null" }, format = "date" }
        },
        required = new[] { "periodDays", "dateTo" }
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
            var periodDays = ChatToolJson.ReadInt(document.RootElement, "periodDays", DefaultPeriodDays);
            periodDays = Math.Clamp(periodDays, MinimumPeriodDays, MaximumPeriodDays);
            var rate = await businessQueries.GetFiscalReturnRateAsync(
                periodDays,
                ChatToolJson.ReadNullableDate(document.RootElement, "dateTo"),
                cancellationToken);
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, 1);
            return ChatToolResult.Ok(rate, 1);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para taxa de devolução.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível consultar taxa de devolução agora.");
        }
    }
}

public sealed class SearchProductsChatTool(
    IBusinessChatQueryService businessQueries,
    IOptions<AssistantOptions> options,
    ILogger<SearchProductsChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 10;
    private const int MinimumSearchTermLength = 2;
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "search_products";

    public string Description =>
        "Pesquisa produtos por nome, descrição, código externo, código ERP, código operacional ou GTIN, retornando cadastro e estoque consolidado atual com quantidades e valores quando disponíveis.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            searchTerm = new { type = "string", description = "Nome, descrição, código externo, código ERP, código operacional ou GTIN do produto." },
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
            var searchTerm = SearchRoutesChatTool.ReadString(document.RootElement, "searchTerm");
            if (searchTerm.Length < MinimumSearchTermLength)
            {
                return ChatToolResult.Fail("Informe ao menos 2 caracteres para pesquisar produtos.");
            }

            var products = await businessQueries.SearchProductsAsync(
                searchTerm,
                SearchRoutesChatTool.ReadLimit(document.RootElement, DefaultLimit, assistantOptions.MaximumGeneralSearchResults),
                cancellationToken);
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, products.Count);
            return ChatToolResult.Ok(products, products.Count);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para pesquisa de produtos.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível pesquisar produtos agora.");
        }
    }
}

public sealed class GetProductDetailsChatTool(
    IBusinessChatQueryService businessQueries,
    ILogger<GetProductDetailsChatTool> logger) : IChatTool
{
    private const int InventoryHistoryLimit = 10;
    private const int ProductionHistoryLimit = 30;
    private const int FiscalItemsLimit = 10;

    public string Name => "get_product_details";

    public string Description =>
        "Consulta o cadastro completo do produto, estoque atual e histórico com quantidades e valores, produção diária recente e itens fiscais recentes com preço, total, tributos e referências operacionais.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            productId = new { type = "string", format = "uuid", description = "Identificador do produto." }
        },
        required = new[] { "productId" }
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
            var productIdValue = SearchRoutesChatTool.ReadString(document.RootElement, "productId");
            if (!Guid.TryParse(productIdValue, out var productId))
            {
                return ChatToolResult.Fail("Identificador de produto inválido.");
            }

            var details = await businessQueries.GetProductDetailsAsync(
                productId,
                InventoryHistoryLimit,
                ProductionHistoryLimit,
                FiscalItemsLimit,
                cancellationToken);
            object payload = details is null
                ? new { found = false, message = "Produto não encontrado." }
                : new { found = true, details };
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, details is null ? 0 : 1);
            return ChatToolResult.Ok(payload, details is null ? 0 : 1);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para detalhe do produto.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível consultar o produto agora.");
        }
    }
}

public sealed class GetInventorySummaryChatTool(
    IBusinessChatQueryService businessQueries,
    ILogger<GetInventorySummaryChatTool> logger) : IChatTool
{
    public string Name => "get_inventory_summary";

    public string Description =>
        "Consulta totais de quantidade e valor do estoque atual, rupturas, comprometimento, última produção, saída e saldo operacional.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new { },
        required = Array.Empty<string>()
    };

    public async Task<ChatToolResult> ExecuteAsync(
        string argumentsJson,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var _ = JsonDocument.Parse(argumentsJson);
            var summary = await businessQueries.GetInventorySummaryAsync(cancellationToken);
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, 1);
            return ChatToolResult.Ok(summary, 1);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para resumo de estoque.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível consultar resumo de estoque agora.");
        }
    }
}

public sealed class ListInventoryPositionsChatTool(
    IBusinessChatQueryService businessQueries,
    IOptions<AssistantOptions> options,
    ILogger<ListInventoryPositionsChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 10;
    private static readonly HashSet<string> SupportedStatuses = ["AVAILABLE", "STOCKOUT"];
    private static readonly HashSet<string> SupportedSorts = ["available_asc", "committed_desc", "committed_percent_desc"];
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "list_inventory_positions";

    public string Description =>
        "Lista posições do estoque atual por produto, busca, armazém, status de saldo ou ordenação, incluindo quantidades, valor em estoque e valor comprometido.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            searchTerm = new { type = new[] { "string", "null" }, description = "Nome, descrição, código externo, código ERP, código operacional ou GTIN do produto." },
            productId = new { type = new[] { "string", "null" }, format = "uuid" },
            warehouse = new { type = new[] { "string", "null" }, description = "Código do armazém." },
            status = new { type = new[] { "string", "null" }, @enum = new object?[] { "AVAILABLE", "STOCKOUT", null } },
            sort = new { type = "string", @enum = new[] { "available_asc", "committed_desc", "committed_percent_desc" } },
            limit = new { type = "integer", minimum = 1, maximum = assistantOptions.MaximumGeneralSearchResults }
        },
        required = new[] { "searchTerm", "productId", "warehouse", "status", "sort", "limit" }
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
            var productIdValue = ChatToolJson.ReadNullableString(document.RootElement, "productId");
            if (productIdValue is not null && !Guid.TryParse(productIdValue, out _))
            {
                return ChatToolResult.Fail("Identificador de produto inválido.");
            }

            var status = ChatToolJson.ReadNullableString(document.RootElement, "status")?.ToUpperInvariant();
            if (status is not null && !SupportedStatuses.Contains(status))
            {
                return ChatToolResult.Fail("Status de estoque inválido.");
            }

            var sort = SearchRoutesChatTool.ReadString(document.RootElement, "sort").ToLowerInvariant();
            if (!SupportedSorts.Contains(sort))
            {
                sort = "available_asc";
            }

            var query = new BusinessChatInventoryPositionQuery(
                ChatToolJson.ReadNullableString(document.RootElement, "searchTerm"),
                productIdValue is null ? null : Guid.Parse(productIdValue),
                ChatToolJson.ReadNullableString(document.RootElement, "warehouse"),
                status,
                sort,
                SearchRoutesChatTool.ReadLimit(document.RootElement, DefaultLimit, assistantOptions.MaximumGeneralSearchResults));
            var positions = await businessQueries.ListInventoryPositionsAsync(query, cancellationToken);
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, positions.Count);
            return ChatToolResult.Ok(positions, positions.Count);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para posições de estoque.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível listar posições de estoque agora.");
        }
    }
}

public sealed class ListStockoutProductsChatTool(
    IBusinessChatQueryService businessQueries,
    IOptions<AssistantOptions> options,
    ILogger<ListStockoutProductsChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 10;
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "list_stockout_products";

    public string Description =>
        "Lista produtos em ruptura no estoque atual, consolidados por produto em todos os armazéns, com quantidades, valores e posições afetadas.";

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
            var products = await businessQueries.ListStockoutProductsAsync(
                SearchRoutesChatTool.ReadLimit(document.RootElement, DefaultLimit, assistantOptions.MaximumGeneralSearchResults),
                cancellationToken);
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, products.Count);
            return ChatToolResult.Ok(products, products.Count);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para listagem de rupturas.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível listar rupturas agora.");
        }
    }
}

public sealed class GetProductionSummaryChatTool(
    IBusinessChatQueryService businessQueries,
    ILogger<GetProductionSummaryChatTool> logger) : IChatTool
{
    public string Name => "get_production_summary";

    public string Description =>
        "Consulta o resumo de produção: última data publicada, produção, saída, saldo operacional e totais do mês atual.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new { },
        required = Array.Empty<string>()
    };

    public async Task<ChatToolResult> ExecuteAsync(
        string argumentsJson,
        ChatExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var _ = JsonDocument.Parse(argumentsJson);
            var summary = await businessQueries.GetProductionSummaryAsync(cancellationToken);
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, 1);
            return ChatToolResult.Ok(summary, 1);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para resumo de produção.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível consultar produção agora.");
        }
    }
}

public sealed class ListProductionRecordsChatTool(
    IBusinessChatQueryService businessQueries,
    IOptions<AssistantOptions> options,
    ILogger<ListProductionRecordsChatTool> logger) : IChatTool
{
    private const int DefaultLimit = 10;
    private static readonly HashSet<string> SupportedSorts = ["date_desc", "production_desc", "production_asc"];
    private readonly AssistantOptions assistantOptions = options.Value;

    public string Name => "list_production_records";

    public string Description =>
        "Lista registros de produção e saída por produto, texto de busca e período, usando o controle diário publicado.";

    public object GetParameterSchema() => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            searchTerm = new { type = new[] { "string", "null" }, description = "Nome, código ERP ou código operacional do produto." },
            productId = new { type = new[] { "string", "null" }, format = "uuid" },
            dateFrom = new { type = new[] { "string", "null" }, format = "date" },
            dateTo = new { type = new[] { "string", "null" }, format = "date" },
            sort = new { type = "string", @enum = new[] { "date_desc", "production_desc", "production_asc" } },
            limit = new { type = "integer", minimum = 1, maximum = assistantOptions.MaximumGeneralSearchResults }
        },
        required = new[] { "searchTerm", "productId", "dateFrom", "dateTo", "sort", "limit" }
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
            var productIdValue = ChatToolJson.ReadNullableString(document.RootElement, "productId");
            if (productIdValue is not null && !Guid.TryParse(productIdValue, out _))
            {
                return ChatToolResult.Fail("Identificador de produto inválido.");
            }

            var sort = SearchRoutesChatTool.ReadString(document.RootElement, "sort").ToLowerInvariant();
            if (!SupportedSorts.Contains(sort))
            {
                sort = "date_desc";
            }

            var query = new BusinessChatProductionRecordQuery(
                ChatToolJson.ReadNullableString(document.RootElement, "searchTerm"),
                productIdValue is null ? null : Guid.Parse(productIdValue),
                ChatToolJson.ReadNullableDate(document.RootElement, "dateFrom"),
                ChatToolJson.ReadNullableDate(document.RootElement, "dateTo"),
                sort,
                SearchRoutesChatTool.ReadLimit(document.RootElement, DefaultLimit, assistantOptions.MaximumGeneralSearchResults));
            var records = await businessQueries.ListProductionRecordsAsync(query, cancellationToken);
            ChatToolLogging.LogSuccess(logger, Name, context, startedAt, records.Count);
            return ChatToolResult.Ok(records, records.Count);
        }
        catch (JsonException)
        {
            return ChatToolResult.Fail("Argumentos inválidos para listagem de produção.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao executar ferramenta {ToolName}.", Name);
            return ChatToolResult.Fail("Não foi possível listar produção agora.");
        }
    }
}

internal static class ChatToolJson
{
    public static string? ReadNullableString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static DateOnly? ReadNullableDate(JsonElement root, string propertyName)
    {
        var text = ReadNullableString(root, propertyName);
        if (text is null)
        {
            return null;
        }

        return DateOnly.TryParse(text, out var date) ? date : throw new JsonException();
    }

    public static int ReadInt(JsonElement root, string propertyName, int defaultValue) =>
        root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : defaultValue;
}

internal static class ChatToolLogging
{
    public static void LogSuccess(
        ILogger logger,
        string toolName,
        ChatExecutionContext context,
        long startedAt,
        int recordCount) =>
        logger.LogInformation(
            "Chat tool {ToolName} executada para usuário {UserId} em {ElapsedMs} ms com {RecordCount} registros.",
            toolName,
            context.UserId,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            recordCount);
}
