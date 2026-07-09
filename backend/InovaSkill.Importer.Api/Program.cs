using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Api.Hangfire;
using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.BackgroundJobs;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddImportInfrastructure(builder.Configuration);
builder.Services.Configure<ImportHangfireOptions>(
    builder.Configuration.GetSection(ImportHangfireOptions.SectionName));
builder.Services.AddImportHangfire(builder.Configuration);
builder.Services.Configure<JwtAuthOptions>(builder.Configuration.GetSection(JwtAuthOptions.SectionName));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<PasswordHasher<AppUser>>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = RouteImportCodes.MaximumUploadSizeBytes;
});
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ImportDbContext>();
    await db.Database.MigrateAsync();
    await new DefaultUserSeeder(
        db,
        scope.ServiceProvider.GetRequiredService<PasswordHasher<AppUser>>())
        .EnsureDefaultUsersAsync();

    var existing = await db.DetectorDefinitions
        .FirstOrDefaultAsync(x => x.Code == DetectorCodes.CustomerPurchaseDrop);
    if (existing is null)
    {
        var now = DateTime.UtcNow;

        var oldSample = await db.DetectorDefinitions
            .FirstOrDefaultAsync(x => x.Code == "DEV_SAMPLE_DETECTOR");

        if (oldSample is not null)
        {
            oldSample.Code = DetectorCodes.CustomerPurchaseDrop;
            oldSample.Name = "Cliente fora do padrão de compra";
            oldSample.Description = "Identifica clientes com volume de compras significativamente abaixo da média histórica mensal, considerando os últimos 30 dias contra os 60 dias anteriores.";
            oldSample.UpdatedAt = now;
        }
        else
        {
            db.DetectorDefinitions.Add(new DetectorDefinition
            {
                Id = Guid.NewGuid(),
                Code = DetectorCodes.CustomerPurchaseDrop,
                Name = "Cliente fora do padrão de compra",
                Description = "Identifica clientes com volume de compras significativamente abaixo da média histórica mensal, considerando os últimos 30 dias contra os 60 dias anteriores.",
                Status = DetectorStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync();
    }
}

if (!builder.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

app.UseCors("frontend");
var hangfireOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImportHangfireOptions>>().Value;
if (hangfireOptions.Enabled && hangfireOptions.Dashboard.Enabled)
{
    app.UseHangfireDashboard(
        hangfireOptions.Dashboard.Path,
        new DashboardOptions
        {
            Authorization =
            [
                new HangfireDashboardAuthorizationFilter(
                    app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImportHangfireOptions>>(),
                    app.Environment)
            ]
        });
}
app.UseMiddleware<JwtAuthMiddleware>();
app.MapControllers();
app.Run();
