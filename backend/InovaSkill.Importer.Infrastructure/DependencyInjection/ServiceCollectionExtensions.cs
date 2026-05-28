using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Processing;
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
        services.AddScoped<IPreProcessorRuleEngine, PreProcessorRuleEngine>();
        services.AddScoped<IRowValidator, RowValidator>();
        services.AddScoped<IFileImportPipelineProcessor, FileImportPipelineProcessor>();
        services.AddScoped<IFileJobService, FileJobService>();
        services.AddScoped<IFileUploadService, FileUploadService>();
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddSingleton<IFileJobQueue, RedisFileJobQueue>();
        return services;
    }
}


