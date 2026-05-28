using Hangfire;
using Hangfire.PostgreSql;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Persistence.Bootstrap;
using InovaSkill.Importer.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddImportInfrastructure(builder.Configuration);
var parallelWorkers = builder.Configuration.GetValue<int?>("ImportProcessing:ParallelWorkers") ?? 4;
var dispatchIntervalSeconds = Math.Max(5, builder.Configuration.GetValue<int?>("ImportProcessing:DispatchIntervalSeconds") ?? 15);
var hangfireConnection = builder.Configuration.GetConnectionString("HangfireDb")
    ?? builder.Configuration.GetConnectionString("ImportDb")
    ?? "Host=localhost;Port=5432;Database=inovaskill_importer;Username=postgres;Password=postgres";

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
    {
        options.UseNpgsqlConnection(hangfireConnection);
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Math.Max(1, parallelWorkers);
    options.Queues = ["default"];
    options.SchedulePollingInterval = TimeSpan.FromSeconds(dispatchIntervalSeconds);
});

builder.Services.AddScoped<FileImportHangfireJob>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        await DbSchemaBootstrapper.EnsureProgressColumnsAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARN] DB bootstrap failed in worker startup: {ex.Message}");
    }
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    for (var i = 1; i <= Math.Max(1, parallelWorkers); i++)
    {
        var recurringId = $"process-pending-file-job-worker-{i}";
        recurringJobManager.AddOrUpdate<FileImportHangfireJob>(
            recurringId,
            job => job.ProcessOnePendingFileJob(),
            $"*/{dispatchIntervalSeconds} * * * * *");
    }
}

await host.RunAsync();
