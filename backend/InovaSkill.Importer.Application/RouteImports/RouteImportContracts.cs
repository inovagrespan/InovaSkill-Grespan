using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Application.WhatsApp;
using System.Text.Json;

namespace InovaSkill.Importer.Application.RouteImports;

public static class RouteImportCodes
{
    public const string DataSource = "ROUTES_BY_CITY";
    public const string DataSourceName = "Rotas por Cidades";
    public const string DataSourceType = "EXCEL";
    public const string ProcessorKey = "logistics-routes";
    public const string JobType = "PROCESS_IMPORT";
    public const long InitialVersion = 1;
    public const int WorkerExecutionTimeoutMinutes = 30;
    public const long MaximumUploadSizeBytes = 100L * 1024 * 1024;
}

public static class CustomerImportCodes
{
    public const string DataSource = "CUSTOMERS";
    public const string DataSourceName = "Cadastro de Clientes";
    public const string DataSourceType = "EXCEL";
    public const string ProcessorKey = "customers";
}

public static class CustomerRouteAssignmentImportCodes
{
    public const string DataSource = "CUSTOMER_ROUTE_ASSIGNMENTS";
    public const string DataSourceName = "Vínculos de Clientes e Rotas";
    public const string DataSourceType = "EXCEL";
    public const string ProcessorKey = "customer-route-assignments";
    public const string CustomerCorrectionField = "customer_id";
    public const string RouteCorrectionField = "route_id";
}

public static class FiscalImportCodes
{
    public const string DataSource = "FISCAL_MOVEMENTS";
    public const string DataSourceName = "Movimentações Fiscais";
    public const string DataSourceType = "EXCEL";
    public const string ProcessorKey = "fiscal-movements";
}

public static class ProductImportCodes
{
    public const string DataSource = "PRODUCTS";
    public const string DataSourceName = "Cadastro de Produtos";
    public const string DataSourceType = "EXCEL";
    public const string ProcessorKey = "products";
}

public static class InventoryCurrentImportCodes
{
    public const string DataSource = "INVENTORY_CURRENT";
    public const string DataSourceName = "Estoque Atual";
    public const string DataSourceType = "EXCEL";
    public const string ProcessorKey = "inventory-current";
}

public static class DailyInventoryImportCodes
{
    public const string DataSource = "DAILY_INVENTORY";
    public const string DataSourceName = "Controle Diário de Estoque";
    public const string DataSourceType = "EXCEL";
    public const string ProcessorKey = "daily-inventory";
}

public static class ProductCodeNormalizer
{
    public static string NormalizeOperationalCode(string value)
    {
        var compact = value.Trim().ToUpperInvariant();
        return compact.StartsWith('V') ? compact[1..] : compact;
    }
}

public static class OperationalJobCodes
{
    public const string ProcessImport = RouteImportCodes.JobType;
    public const string MunicipalityCoordinateEnrichment = "MUNICIPALITY_COORDINATE_ENRICHMENT";
    public const string CustomerRegistrationAddressEnrichment = "CUSTOMER_REGISTRATION_ADDRESS_ENRICHMENT";
    public const string CustomerAddressCoordinateEnrichment = "CUSTOMER_ADDRESS_COORDINATE_ENRICHMENT";
    public const string WhatsAppMessageProcessing = WhatsAppJobCodes.MessageProcessing;
    public const int WorkerExecutionTimeoutMinutes = 30;
}

public static class CustomerRegistrationAddressCustomerStatuses
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
    public const string All = "ALL";

    public static string Read(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("customerStatus", out var value) || value.ValueKind == JsonValueKind.Null)
            return Active;
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ArgumentException("$.customerStatus deve ser ACTIVE, INACTIVE ou ALL.");
        var normalized = value.GetString()!.Trim().ToUpperInvariant();
        return normalized is Active or Inactive or All
            ? normalized
            : throw new ArgumentException("$.customerStatus deve ser ACTIVE, INACTIVE ou ALL.");
    }
}

public static class BackgroundJobQueues
{
    public const string Imports = "imports";
    public const string Default = "default";
}

public sealed record OperationalJobDefinition(
    string JobType,
    string DisplayName,
    string Description,
    bool ManualRunAllowed,
    bool ScheduleAllowed,
    bool AllowConcurrentRuns,
    string Queue,
    int ContractVersion,
    string ExampleParametersJson);

public static class OperationalJobCatalog
{
    public static readonly OperationalJobDefinition MunicipalityCoordinateEnrichment = new(
        OperationalJobCodes.MunicipalityCoordinateEnrichment,
        "Enriquecer coordenadas de municípios",
        "Busca municípios de clientes sem coordenadas e salva latitude/longitude por cidade.",
        ManualRunAllowed: true,
        ScheduleAllowed: true,
        AllowConcurrentRuns: true,
        BackgroundJobQueues.Default,
        ContractVersion: 1,
        ExampleParametersJson: "{\"importId\":\"00000000-0000-0000-0000-000000000000\",\"reprocessFailed\":false}");

    public static readonly OperationalJobDefinition CustomerRegistrationAddressEnrichment = new(
        OperationalJobCodes.CustomerRegistrationAddressEnrichment,
        "Enriquecer endereços cadastrais de clientes",
        "Consulta CNPJs de um snapshot de clientes na BrasilAPI e salva os endereços cadastrais ainda ausentes.",
        ManualRunAllowed: true,
        ScheduleAllowed: true,
        AllowConcurrentRuns: false,
        BackgroundJobQueues.Default,
        ContractVersion: 1,
        ExampleParametersJson: "{\"customerStatus\":\"ACTIVE\",\"refreshResolved\":false}");

    public static readonly OperationalJobDefinition CustomerAddressCoordinateEnrichment = new(
        OperationalJobCodes.CustomerAddressCoordinateEnrichment,
        "Geocodificar endereços de clientes",
        "Converte endereços cadastrais resolvidos em latitude/longitude pelo Nominatim.",
        ManualRunAllowed: true,
        ScheduleAllowed: false,
        AllowConcurrentRuns: false,
        BackgroundJobQueues.Default,
        ContractVersion: 1,
        ExampleParametersJson: "{\"customerStatus\":\"ACTIVE\",\"reprocessFailed\":false}");

    public static readonly OperationalJobDefinition WhatsAppMessageProcessing = new(
        OperationalJobCodes.WhatsAppMessageProcessing,
        "Processar mensagem do WhatsApp",
        "Transcreve quando necessário, consulta o assistente e envia a resposta ao remetente autorizado.",
        ManualRunAllowed: false,
        ScheduleAllowed: false,
        AllowConcurrentRuns: true,
        BackgroundJobQueues.Default,
        ContractVersion: 1,
        ExampleParametersJson: "{\"receiptId\":\"00000000-0000-0000-0000-000000000000\"}");

    public static readonly OperationalJobDefinition ProcessImport = new(
        OperationalJobCodes.ProcessImport,
        "Processar arquivo importado",
        "Processa um arquivo previamente recebido e associado a uma fonte de dados.",
        ManualRunAllowed: false,
        ScheduleAllowed: false,
        AllowConcurrentRuns: false,
        BackgroundJobQueues.Imports,
        ContractVersion: 1,
        ExampleParametersJson: "{\"importId\":\"00000000-0000-0000-0000-000000000000\"}");

    private static readonly IReadOnlyDictionary<string, OperationalJobDefinition> Definitions =
        new[] { ProcessImport, MunicipalityCoordinateEnrichment, CustomerRegistrationAddressEnrichment, CustomerAddressCoordinateEnrichment, WhatsAppMessageProcessing }
            .ToDictionary(definition => definition.JobType, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<OperationalJobDefinition> All { get; } = Definitions.Values.ToArray();

    public static bool TryGet(string jobType, out OperationalJobDefinition definition) =>
        Definitions.TryGetValue(jobType, out definition!);

    public static OperationalJobDefinition GetRequired(string jobType) =>
        TryGet(jobType, out var definition)
            ? definition
            : throw new InvalidOperationException($"Job desconhecido: {jobType}.");
}

public interface IDataSourceProcessor
{
    string SourceCode { get; }
    Task ProcessAsync(Guid importId, CancellationToken cancellationToken);
}

public interface IOperationalJobProcessor
{
    string JobType { get; }
    Task ProcessAsync(Guid relatedEntityId, CancellationToken cancellationToken);
}

public interface IProgressReportingOperationalJobProcessor : IOperationalJobProcessor
{
    Task ProcessAsync(Guid relatedEntityId, Guid jobExecutionId, CancellationToken cancellationToken);
}

public interface IOperationalJobQueue
{
    Task<Guid?> TryQueueAsync(
        string jobType,
        Guid relatedEntityId,
        CancellationToken cancellationToken);
}

public interface IBackgroundJobDispatcher
{
    string EnqueueImport(Guid importId, Guid jobExecutionId);

    string EnqueueOperationalJob(Guid jobExecutionId);
}

public interface IImportProcessingService
{
    Task ProcessAsync(
        Guid importId,
        Guid jobExecutionId,
        CancellationToken cancellationToken);
}

public interface IOperationalJobProcessingService
{
    Task ProcessAsync(
        Guid jobExecutionId,
        CancellationToken cancellationToken);
}

public interface IImportLifecycleService
{
    Task<RouteImport> CreateAsync(
        Guid dataSourceId,
        string fileName,
        string filePath,
        CancellationToken cancellationToken);

    Task<bool> TryActivateAsync(Guid importId, CancellationToken cancellationToken);
}

public interface IImportFileStorage
{
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}

public interface ISpreadsheetDataSourceDetector
{
    string Detect(Stream content);
}

public sealed class StructuralImportException(string message, Exception? innerException = null)
    : Exception(message, innerException);
