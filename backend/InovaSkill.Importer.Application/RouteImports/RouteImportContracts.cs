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

public sealed record ProcessImport(Guid ImportId, Guid JobExecutionId);

public interface IDataSourceProcessor
{
    string SourceCode { get; }
    Task ProcessAsync(Guid importId, CancellationToken cancellationToken);
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
