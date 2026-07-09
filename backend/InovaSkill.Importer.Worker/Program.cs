using InovaSkill.Importer.Infrastructure.BackgroundJobs;
using InovaSkill.Importer.Infrastructure.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddImportInfrastructure(builder.Configuration);
builder.Services.AddImportHangfire(builder.Configuration);
builder.Services.AddImportHangfireServers(builder.Configuration);

await builder.Build().RunAsync();
