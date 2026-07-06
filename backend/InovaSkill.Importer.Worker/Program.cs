using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.RouteImports;
using InovaSkill.Importer.Application.RouteImports;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddImportInfrastructure(builder.Configuration);

var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis não foi configurada.");
builder.UseWolverine(options =>
{
    options.ServiceName = "InovaSkill.Importer.Worker";
    options.DefaultExecutionTimeout = TimeSpan.FromMinutes(RouteImportCodes.WorkerExecutionTimeoutMinutes);
    options.Discovery.IncludeAssembly(typeof(ProcessImportHandler).Assembly);
    options.UseRedisTransport(redisConnection).AutoProvision();
    options.ListenToRedisStream("route-imports", "route-import-workers")
        .ProcessInline()
        .StartFromBeginning();
    options.Policies.OnException<Exception>().ScheduleRetry(
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2));
});

await builder.Build().RunAsync();
