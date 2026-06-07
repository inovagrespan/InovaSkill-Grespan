using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.Persistence;
using InovaSkill.Importer.Infrastructure.Persistence.Bootstrap;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

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
    await EnsureDefaultAdminUserAsync(
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

static async Task EnsureDefaultAdminUserAsync(ImportDbContext db, PasswordHasher<AppUser> passwordHasher)
{
    const string adminUserName = "admin";
    const string adminEmail = "admin@local.test";
    const string adminPassword = "admin";

    var existingAdmin = await db.AppUsers.FirstOrDefaultAsync(
        x => x.Email == adminEmail || x.Name.ToLower() == adminUserName);
    if (existingAdmin is not null)
    {
        var verification = passwordHasher.VerifyHashedPassword(existingAdmin, existingAdmin.PasswordHash, adminPassword);
        if (verification == PasswordVerificationResult.Failed)
        {
            existingAdmin.PasswordHash = passwordHasher.HashPassword(existingAdmin, adminPassword);
            await db.SaveChangesAsync();
        }

        return;
    }

    var admin = new AppUser
    {
        Name = adminUserName,
        Email = adminEmail,
        CreatedAt = DateTime.UtcNow
    };
    admin.PasswordHash = passwordHasher.HashPassword(admin, adminPassword);

    db.AppUsers.Add(admin);
    await db.SaveChangesAsync();
}
