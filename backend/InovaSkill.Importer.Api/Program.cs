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
    await EnsureDefaultUsersAsync(
        db,
        scope.ServiceProvider.GetRequiredService<PasswordHasher<AppUser>>());
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

static async Task EnsureDefaultUsersAsync(ImportDbContext db, PasswordHasher<AppUser> passwordHasher)
{
    var defaults = new[]
    {
        new SeedUser("admin", "admin@local.test", "admin", AppUserRoles.Admin),
        new SeedUser("grespan", "grespan@local.test", "inova2026", AppUserRoles.Gestor)
    };

    foreach (var seed in defaults)
    {
        var normalizedName = seed.Name.ToLowerInvariant();
        var normalizedEmail = seed.Email.ToLowerInvariant();
        var existingUser = await db.AppUsers.FirstOrDefaultAsync(
            x => x.Email == normalizedEmail || x.Name.ToLower() == normalizedName);

        if (existingUser is null)
        {
            var createdUser = new AppUser
            {
                Name = seed.Name,
                Email = normalizedEmail,
                Role = seed.Role,
                CreatedAt = DateTime.UtcNow
            };
            createdUser.PasswordHash = passwordHasher.HashPassword(createdUser, seed.Password);
            db.AppUsers.Add(createdUser);
            continue;
        }

        existingUser.Name = seed.Name;
        existingUser.Email = normalizedEmail;
        existingUser.Role = seed.Role;

        var verification = passwordHasher.VerifyHashedPassword(existingUser, existingUser.PasswordHash, seed.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, seed.Password);
        }
    }

    await db.SaveChangesAsync();
}

internal sealed record SeedUser(string Name, string Email, string Password, string Role);
