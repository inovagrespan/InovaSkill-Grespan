using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Api.Assistant;
using InovaSkill.Importer.Api.Hangfire;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.BackgroundJobs;
using InovaSkill.Importer.Infrastructure.DependencyInjection;
using InovaSkill.Importer.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.Configure<AssistantOptions>(options =>
{
    builder.Configuration.GetSection(AssistantOptions.SectionName).Bind(options);
    options.OpenAiApiKey = builder.Configuration["OPENAI_API_KEY"] ?? options.OpenAiApiKey;
});
builder.Services.AddScoped<IChatModelClient, OpenAiChatModelClient>();
builder.Services.AddScoped<IChatHistoryStore, ChatHistoryStore>();
builder.Services.AddScoped<IChatTool, SearchRoutesChatTool>();
builder.Services.AddScoped<IChatTool, GetRouteDetailsChatTool>();
builder.Services.AddScoped<IChatTool, GetCriticalRoutesChatTool>();
builder.Services.AddScoped<IChatTool, ListRoutesByOccupancyChatTool>();
builder.Services.AddScoped<IChatTool, GetRouteCitiesChatTool>();
builder.Services.AddScoped<IChatTool, GetRouteCustomersChatTool>();
builder.Services.AddScoped<IChatTool, GetLatestGlobalRouteOptimizationChatTool>();
builder.Services.AddScoped<IChatTool, GetLatestRouteOptimizationChatTool>();
builder.Services.AddScoped<IChatTool, RequestGlobalRouteOptimizationChatTool>();
builder.Services.AddScoped<IChatTool, SearchCustomersChatTool>();
builder.Services.AddScoped<IChatTool, GetCustomerConsumptionSummaryChatTool>();
builder.Services.AddScoped<IChatTool, ListRecentFiscalDocumentsChatTool>();
builder.Services.AddScoped<IChatTool, GetFiscalReturnRateChatTool>();
builder.Services.AddScoped<IChatTool, SearchProductsChatTool>();
builder.Services.AddScoped<IChatTool, GetProductDetailsChatTool>();
builder.Services.AddScoped<IChatTool, GetInventorySummaryChatTool>();
builder.Services.AddScoped<IChatTool, ListInventoryPositionsChatTool>();
builder.Services.AddScoped<IChatTool, ListStockoutProductsChatTool>();
builder.Services.AddScoped<IChatTool, GetProductionSummaryChatTool>();
builder.Services.AddScoped<IChatTool, ListProductionRecordsChatTool>();
builder.Services.AddScoped<BusinessAssistantService>();
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
app.UseMiddleware<ApiAuthorizationMiddleware>();
app.MapControllers();
app.Run();
