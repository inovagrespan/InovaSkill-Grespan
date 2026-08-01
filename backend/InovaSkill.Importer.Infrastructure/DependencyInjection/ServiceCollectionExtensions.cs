using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Application.Caching;
using InovaSkill.Importer.Infrastructure.Caching;
using InovaSkill.Importer.Infrastructure.BackgroundJobs;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.AddMemoryCache();
        services.AddSingleton<ICacheStore, MemoryCacheStore>();
        services.AddSingleton<IApplicationCache, ResilientApplicationCache>();
        services.Configure<RouteOptimizationOptions>(
            configuration.GetSection(RouteOptimizationOptions.SectionName));
        services.AddHttpClient<OsrmDistanceMatrixProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RouteOptimizationOptions>>().Value;
            client.BaseAddress = OsrmDistanceMatrixProvider.NormalizeBaseUrl(options.OsrmBaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
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
        services.AddScoped<IRouteOptimizationJobDispatcher, HangfireBackgroundJobDispatcher>();
        services.AddScoped<IImportProcessingService, ImportProcessingService>();
        services.AddScoped<IOperationalJobProcessingService, OperationalJobProcessingService>();
        services.AddScoped<IRouteChatQueryService, RouteChatQueryService>();
        services.AddScoped<IBusinessChatQueryService, BusinessChatQueryService>();
        services.AddScoped<IDistanceMatrixProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RouteOptimizationOptions>>().Value;
            return string.Equals(options.DistanceProvider, RouteDistanceProviderNames.Osrm, StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<OsrmDistanceMatrixProvider>()
                : new GeographicDistanceMatrixProvider();
        });
        services.AddScoped<IRouteOptimizationSolver, SingleRouteOptimizationSolver>();
        services.AddScoped<IRouteOptimizationSolver, GlobalRouteOptimizationSolver>();
        services.AddScoped<IRouteOptimizationService, RouteOptimizationService>();
        services.AddScoped<IRouteOptimizationProcessingService, RouteOptimizationProcessingService>();
        services.AddScoped<IRouteCustomerAssignmentSynchronizer, RouteCustomerAssignmentSynchronizer>();
        services.AddScoped<IDataSourceProcessor, RoutesByCityProcessor>();
        services.AddScoped<IDataSourceProcessor, CustomersProcessor>();
        services.AddScoped<IDataSourceProcessor, FiscalMovementsProcessor>();
        services.AddScoped<IDataSourceProcessor, ProductsProcessor>();
        services.AddScoped<IDataSourceProcessor, InventoryCurrentProcessor>();
        services.AddScoped<IDataSourceProcessor, DailyInventoryProcessor>();
        services.AddScoped<IOperationalJobProcessor, MunicipalityCoordinateEnrichmentProcessor>();
        return services;
    }
}
