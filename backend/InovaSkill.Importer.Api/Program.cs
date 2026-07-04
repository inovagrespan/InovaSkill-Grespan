using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Api.Realtime;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Persistence.Bootstrap;
using InovaSkill.Importer.Infrastructure.Processing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddImportInfrastructure(builder.Configuration);
builder.Services.AddScoped<IFileJobProgressNotifier, RedisFileJobProgressNotifier>();
builder.Services.AddHostedService<RedisFileJobProgressBroadcastService>();
builder.Services.Configure<JwtAuthOptions>(builder.Configuration.GetSection(JwtAuthOptions.SectionName));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<PasswordHasher<AppUser>>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.MigrateAsync();
    await DbSchemaBootstrapper.EnsureProgressColumnsAsync(db);
    await new DefaultUserSeeder(
        db,
        scope.ServiceProvider.GetRequiredService<PasswordHasher<AppUser>>())
        .EnsureDefaultUsersAsync();
}

var disableHttpsRedirection = builder.Configuration.GetValue<bool>("DisableHttpsRedirection");
if (!disableHttpsRedirection)
{
    app.UseHttpsRedirection();
}
app.UseCors("frontend");
app.UseMiddleware<JwtAuthMiddleware>();
app.MapControllers();
app.MapHub<FileJobProgressHub>("/hubs/file-jobs");
app.MapGet("/api/_debug/routes", (IEnumerable<EndpointDataSource> endpointSources) =>
{
    var routes = endpointSources
        .SelectMany(s => s.Endpoints)
        .OfType<RouteEndpoint>()
        .Select(e => e.RoutePattern.RawText)
        .OrderBy(x => x)
        .ToArray();
    return Results.Ok(routes);
});

app.Run();
