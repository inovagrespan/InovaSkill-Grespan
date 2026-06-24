using System.Security.Claims;
using Hangfire.Dashboard;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Api.Auth;

public sealed class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
            return false;

        var role = httpContext.User.FindFirstValue(ClaimTypes.Role)
            ?? httpContext.User.FindFirstValue("role");

        if (string.Equals(role, AppUserRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, AppUserRoles.AdminSystem, StringComparison.OrdinalIgnoreCase))
            return true;

        var tokenQuery = httpContext.Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tokenQuery))
            return false;

        var tokenService = httpContext.RequestServices.GetRequiredService<JwtTokenService>();
        var principal = tokenService.Validate(tokenQuery);
        if (principal is null)
            return false;

        var tokenRole = principal.FindFirstValue(ClaimTypes.Role)
            ?? principal.FindFirstValue("role");

        return string.Equals(tokenRole, AppUserRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tokenRole, AppUserRoles.AdminSystem, StringComparison.OrdinalIgnoreCase);
    }
}
