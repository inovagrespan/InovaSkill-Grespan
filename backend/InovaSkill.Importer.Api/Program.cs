using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Api.Realtime;
using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddImportInfrastructure(builder.Configuration);
builder.Services.Configure<JwtAuthOptions>(builder.Configuration.GetSection(JwtAuthOptions.SectionName));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<PasswordHasher<AppUser>>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
});
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis não foi configurada.");
builder.Host.UseWolverine(options =>
{
    options.ServiceName = "InovaSkill.Importer.Api";
    options.UseRedisTransport(redisConnection).AutoProvision();
    options.PublishMessage<ProcessImport>().ToRedisStream("route-imports");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
    await db.Database.MigrateAsync();
    await new DefaultUserSeeder(
        db,
        scope.ServiceProvider.GetRequiredService<PasswordHasher<AppUser>>())
        .EnsureDefaultUsersAsync();
}

if (!builder.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

app.UseCors("frontend");
app.UseMiddleware<JwtAuthMiddleware>();
app.MapControllers();
app.Run();
