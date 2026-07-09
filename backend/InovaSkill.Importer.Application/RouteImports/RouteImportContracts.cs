using InovaSkill.Importer.Domain.Entities;

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
    public const string MunicipalityCoordinateEnrichment = "MUNICIPALITY_COORDINATE_ENRICHMENT";
    public const int WorkerExecutionTimeoutMinutes = 30;
}

public sealed record ProcessImport(Guid ImportId, Guid JobExecutionId);

public sealed record ProcessOperationalJob(Guid JobExecutionId);

public sealed record OperationalJobDefinition(
    string JobType,
    string DisplayName,
    string Description,
    bool ManualRunAllowed,
    bool ScheduleAllowed,
    bool AllowConcurrentRuns);

public static class OperationalJobCatalog
{
    public static readonly OperationalJobDefinition MunicipalityCoordinateEnrichment = new(
        OperationalJobCodes.MunicipalityCoordinateEnrichment,
        "Enriquecer coordenadas de municípios",
        "Busca municípios de clientes sem coordenadas e salva latitude/longitude por cidade.",
        ManualRunAllowed: true,
        ScheduleAllowed: true,
        AllowConcurrentRuns: false);

    public static IReadOnlyList<OperationalJobDefinition> All { get; } =
        [MunicipalityCoordinateEnrichment];
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

public interface IOperationalJobQueue
{
    Task<Guid?> TryQueueAsync(
        string jobType,
        Guid relatedEntityId,
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
