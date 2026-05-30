using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Persistence.Bootstrap;
using InovaSkill.Importer.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddImportInfrastructure(builder.Configuration);
builder.Services.AddHostedService<RedisQueueWorkerService>();
builder.Services.AddHostedService<PostImportWorkerService>();

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
}

await host.RunAsync();
