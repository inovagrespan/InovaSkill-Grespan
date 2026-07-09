using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Infrastructure.BackgroundJobs;
using InovaSkill.Importer.Infrastructure.Detection;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InovaSkill.Importer.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddImportInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ImportDb")
            ?? throw new InvalidOperationException("ConnectionStrings:ImportDb não foi configurada.");

        services.AddDbContext<ImportDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IImportFileStorage, LocalImportFileStorage>();
        services.AddScoped<ISpreadsheetDataSourceDetector, SpreadsheetDataSourceDetector>();
        services.AddScoped<RoutesSpreadsheetParser>();
        services.AddScoped<CustomersSpreadsheetParser>();
        services.AddScoped<FiscalMovementsSpreadsheetParser>();
        services.AddScoped<ProductsSpreadsheetParser>();
        services.AddScoped<InventoryCurrentSpreadsheetParser>();
        services.AddScoped<DailyInventorySpreadsheetParser>();
        services.AddScoped<IImportLifecycleService, ImportLifecycleService>();
        services.AddScoped<IMunicipalityCoordinateProvider, EmbeddedMunicipalityCoordinateProvider>();
        services.AddScoped<IOperationalJobQueue, OperationalJobQueue>();
        services.AddScoped<IBackgroundJobDispatcher, HangfireBackgroundJobDispatcher>();
        services.AddScoped<IImportProcessingService, ImportProcessingService>();
        services.AddScoped<IOperationalJobProcessingService, OperationalJobProcessingService>();
        services.AddScoped<IDataSourceProcessor, RoutesByCityProcessor>();
        services.AddScoped<IDataSourceProcessor, CustomersProcessor>();
        services.AddScoped<IDataSourceProcessor, FiscalMovementsProcessor>();
        services.AddScoped<IDataSourceProcessor, ProductsProcessor>();
        services.AddScoped<IDataSourceProcessor, InventoryCurrentProcessor>();
        services.AddScoped<IDataSourceProcessor, DailyInventoryProcessor>();
        services.AddScoped<IOperationalJobProcessor, MunicipalityCoordinateEnrichmentProcessor>();
        services.AddScoped<IDetectorRegistry, DetectorRegistry>();
        services.AddScoped<IDetectionJobDispatcher, HangfireDetectionJobDispatcher>();
        services.AddScoped<IDetectionRunService, DetectionRunService>();
        services.AddScoped<IDetector, CustomerPurchaseDropDetector>();
        services.AddScoped<IDetector, RouteOccupancyDetector>();
        return services;
    }
}
