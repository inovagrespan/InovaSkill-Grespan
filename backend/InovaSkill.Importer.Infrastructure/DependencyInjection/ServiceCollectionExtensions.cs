using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Infrastructure.Mappings;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
using InovaSkill.Importer.Infrastructure.Processing.TransformRules;
using InovaSkill.Importer.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace InovaSkill.Importer.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddImportInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ImportDb")
            ?? "Host=localhost;Port=5432;Database=inovaskill_importer;Username=postgres;Password=postgres";

        services.AddDbContext<ImportDbContext>(opt => opt.UseNpgsql(connectionString));
        services.AddScoped<IFileParserFactory, FileParserFactory>();
        services.AddScoped<IFileTypeDetector, FileTypeDetector>();
        services.AddScoped<IFileSchemaProvider, FileSchemaProvider>();
        services.AddScoped<IPreProcessorTemplateResolver, PreProcessorTemplateResolver>();
        services.AddScoped<IImportPreProcessingPipeline, ImportPreProcessingPipeline>();
        services.AddScoped<IPreProcessorRuleEngine, PreProcessorRuleEngine>();
        services.AddScoped<IImportMappingEngine, ImportMappingEngine>();
        services.AddScoped<ITransformRuleRegistry, TransformRuleRegistry>();
        services.AddScoped<ITransformRule, TrimRule>();
        services.AddScoped<ITransformRule, OnlyDigitsRule>();
        services.AddScoped<ITransformRule, BrazilianCurrencyRule>();
        services.AddScoped<ITransformRule, BrazilianDateRule>();
        services.AddScoped<ITransformRule, UpperCaseRule>();
        services.AddScoped<ITransformRule, LowerCaseRule>();
        services.AddScoped<IRowValidator, RowValidator>();
        services.AddScoped<IFileImportPipelineProcessor, FileImportPipelineProcessor>();
        services.AddScoped<IPostImportProcessor, SalesSummaryProcessor>();
        services.AddScoped<IPostImportProcessor, CustomerSummaryProcessor>();
        services.AddScoped<IFileJobService, FileJobService>();
        services.AddScoped<IFileUploadService, FileUploadService>();
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddSingleton<IFileJobQueue, RedisFileJobQueue>();
        services.AddSingleton<IPostImportJobQueue, RedisPostImportJobQueue>();
        return services;
    }
}
