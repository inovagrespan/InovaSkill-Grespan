using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Application.Caching;
using InovaSkill.Importer.Infrastructure.Caching;
using InovaSkill.Importer.Infrastructure.BackgroundJobs;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.RouteImports;
using InovaSkill.Importer.Infrastructure.WhatsApp;
using InovaSkill.Importer.Application.WhatsApp;
using InovaSkill.Importer.Api.Assistant;
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
        services.AddHttpClient();
        services.Configure<BrasilApiOptions>(configuration.GetSection(BrasilApiOptions.SectionName));
        var brasilApiOptions = configuration.GetSection(BrasilApiOptions.SectionName).Get<BrasilApiOptions>()
            ?? new BrasilApiOptions();
        services.AddHttpClient<ICustomerRegistrationAddressProvider, BrasilApiCustomerRegistrationAddressProvider>(client =>
        {
            client.BaseAddress = new Uri(brasilApiOptions.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, brasilApiOptions.TimeoutSeconds));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(brasilApiOptions.UserAgent);
        });
        services.AddMemoryCache();
        services.AddSingleton<ICacheStore, MemoryCacheStore>();
        services.AddSingleton<IApplicationCache, ResilientApplicationCache>();
        services.Configure<AssistantOptions>(options =>
        {
            configuration.GetSection(AssistantOptions.SectionName).Bind(options);
            options.OpenAiApiKey = configuration["OPENAI_API_KEY"] ?? options.OpenAiApiKey;
        });
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.AddScoped<IImportFileStorage, LocalImportFileStorage>();
        services.AddScoped<ISpreadsheetDataSourceDetector, SpreadsheetDataSourceDetector>();
        services.AddScoped<RoutesSpreadsheetParser>();
        services.AddScoped<CustomersSpreadsheetParser>();
        services.AddScoped<CustomerRouteAssignmentsSpreadsheetParser>();
        services.AddScoped<FiscalMovementsSpreadsheetParser>();
        services.AddScoped<ProductsSpreadsheetParser>();
        services.AddScoped<InventoryCurrentSpreadsheetParser>();
        services.AddScoped<DailyInventorySpreadsheetParser>();
        services.AddScoped<IImportLifecycleService, ImportLifecycleService>();
        services.AddScoped<IMunicipalityCoordinateProvider, EmbeddedMunicipalityCoordinateProvider>();
        services.AddScoped<IOperationalJobQueue, OperationalJobQueue>();
        services.AddScoped<IJobExecutionLauncher, JobExecutionLauncher>();
        services.AddScoped<IJobScheduleDispatcher, JobScheduleDispatcher>();
        services.AddScoped<IScheduledJobLauncher, ScheduledJobLauncher>();
        services.AddScoped<IBackgroundJobDispatcher, HangfireBackgroundJobDispatcher>();
        services.AddScoped<IImportProcessingService, ImportProcessingService>();
        services.AddScoped<IOperationalJobProcessingService, OperationalJobProcessingService>();
        services.AddScoped<IRouteChatQueryService, RouteChatQueryService>();
        services.AddScoped<IBusinessChatQueryService, BusinessChatQueryService>();
        services.AddScoped<IRouteCustomerAssignmentSynchronizer, RouteCustomerAssignmentSynchronizer>();
        services.AddScoped<IDataSourceProcessor, RoutesByCityProcessor>();
        services.AddScoped<IDataSourceProcessor, CustomersProcessor>();
        services.AddScoped<IDataSourceProcessor, CustomerRouteAssignmentsProcessor>();
        services.AddScoped<IDataSourceProcessor, FiscalMovementsProcessor>();
        services.AddScoped<IDataSourceProcessor, ProductsProcessor>();
        services.AddScoped<IDataSourceProcessor, InventoryCurrentProcessor>();
        services.AddScoped<IDataSourceProcessor, DailyInventoryProcessor>();
        services.AddScoped<IOperationalJobProcessor, MunicipalityCoordinateEnrichmentProcessor>();
        services.AddScoped<IOperationalJobProcessor, CustomerRegistrationAddressEnrichmentProcessor>();
        services.AddScoped<IWhatsAppGateway, LocalBaileysWhatsAppGateway>();
        services.AddScoped<IAudioTranscriptionService, OpenAiAudioTranscriptionService>();
        services.AddScoped<IWhatsAppMessageQueue, WhatsAppMessageQueue>();
        services.AddScoped<IOperationalJobProcessor, WhatsAppMessageProcessor>();
        services.AddScoped<IChatModelClient, OpenAiChatModelClient>();
        services.AddScoped<AiConsumptionService>();
        services.AddScoped<IChatHistoryStore, ChatHistoryStore>();
        services.AddScoped<AssistantScopeClassifier>();
        services.AddScoped<KnowledgeMemoryService>();
        services.AddScoped<IChatTool, SearchRoutesChatTool>();
        services.AddScoped<IChatTool, GetRouteDetailsChatTool>();
        services.AddScoped<IChatTool, GetCriticalRoutesChatTool>();
        services.AddScoped<IChatTool, ListRoutesByOccupancyChatTool>();
        services.AddScoped<IChatTool, GetRouteCitiesChatTool>();
        services.AddScoped<IChatTool, GetRouteCustomersChatTool>();
        services.AddScoped<IChatTool, SearchCustomersChatTool>();
        services.AddScoped<IChatTool, GetCustomerConsumptionSummaryChatTool>();
        services.AddScoped<IChatTool, ListRecentFiscalDocumentsChatTool>();
        services.AddScoped<IChatTool, GetFiscalReturnRateChatTool>();
        services.AddScoped<IChatTool, SearchProductsChatTool>();
        services.AddScoped<IChatTool, GetProductDetailsChatTool>();
        services.AddScoped<IChatTool, GetInventorySummaryChatTool>();
        services.AddScoped<IChatTool, ListInventoryPositionsChatTool>();
        services.AddScoped<IChatTool, ListStockoutProductsChatTool>();
        services.AddScoped<IChatTool, GetProductionSummaryChatTool>();
        services.AddScoped<IChatTool, ListProductionRecordsChatTool>();
        services.AddScoped<BusinessAssistantService>();
        return services;
    }
}
