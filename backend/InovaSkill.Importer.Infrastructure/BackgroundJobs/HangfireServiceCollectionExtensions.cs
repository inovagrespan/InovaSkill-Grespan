using Hangfire;
using Hangfire.PostgreSql;
using InovaSkill.Importer.Application.RouteImports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InovaSkill.Importer.Infrastructure.BackgroundJobs;

public static class HangfireServiceCollectionExtensions
{
    public static IServiceCollection AddImportHangfire(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(ImportHangfireOptions.SectionName)
            .Get<ImportHangfireOptions>() ?? new ImportHangfireOptions();
        if (!options.Enabled) return services;

        var connectionString = options.Storage.ConnectionString
            ?? configuration.GetConnectionString("ImportDb")
            ?? throw new InvalidOperationException("ConnectionStrings:ImportDb não foi configurada.");

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                storage => storage.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = options.Storage.SchemaName
                }));

        return services;
    }

    public static IServiceCollection AddImportHangfireServers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(ImportHangfireOptions.SectionName)
            .Get<ImportHangfireOptions>() ?? new ImportHangfireOptions();
        if (!options.Enabled) return services;

        services.AddHangfireServer(server =>
        {
            server.ServerName = $"{Environment.MachineName}:imports";
            server.Queues = [BackgroundJobQueues.Imports];
            server.WorkerCount = NormalizeWorkerCount(options.Workers.Imports);
        });
        services.AddHangfireServer(server =>
        {
            server.ServerName = $"{Environment.MachineName}:route-optimization";
            server.Queues = [BackgroundJobQueues.RouteOptimization];
            server.WorkerCount = NormalizeWorkerCount(options.Workers.RouteOptimization);
        });
        services.AddHangfireServer(server =>
        {
            server.ServerName = $"{Environment.MachineName}:default";
            server.Queues = [BackgroundJobQueues.Default];
            server.WorkerCount = NormalizeWorkerCount(options.Workers.Default);
        });

        return services;
    }

    private static int NormalizeWorkerCount(int configuredWorkerCount) =>
        Math.Max(1, configuredWorkerCount);
}
