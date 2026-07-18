using Hangfire.Dashboard;
using InovaSkill.Importer.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Options;

namespace InovaSkill.Importer.Api.Hangfire;

public sealed class HangfireDashboardAuthorizationFilter(
    IOptions<ImportHangfireOptions> options,
    IWebHostEnvironment environment) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) =>
        environment.IsDevelopment() || options.Value.Dashboard.AllowAnonymous;
}
