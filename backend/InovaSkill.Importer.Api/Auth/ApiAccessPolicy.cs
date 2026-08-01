using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Api.Auth;

public static class ApiAccessPolicy
{
    private static readonly string[] AllApplicationRoles =
    [
        AppUserRoles.Diretor,
        AppUserRoles.Vendas,
        AppUserRoles.Logistica,
        AppUserRoles.Admin,
        AppUserRoles.AdminSystem
    ];

    private static readonly string[] LogisticsRoles =
    [
        AppUserRoles.Diretor,
        AppUserRoles.Logistica,
        AppUserRoles.Admin,
        AppUserRoles.AdminSystem
    ];

    private static readonly string[] AdministratorRoles =
    [
        AppUserRoles.Admin,
        AppUserRoles.AdminSystem
    ];

    public static bool CanAccess(string? role, string path, string method)
    {
        if (path.Equals("/api/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/register", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var normalizedRole = role.Trim().ToLowerInvariant();
        var isMutation = !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method);

        if (StartsWithAny(path, "/api/admin/jobs", "/api/admin/ai-consumption", "/api/admin/knowledge-memories", "/api/route-imports", "/api/import-errors"))
        {
            return AdministratorRoles.Contains(normalizedRole);
        }

        if (path.StartsWith("/api/vehicle-types", StringComparison.OrdinalIgnoreCase))
        {
            return isMutation
                ? normalizedRole is AppUserRoles.Logistica or AppUserRoles.Admin or AppUserRoles.AdminSystem
                : AllApplicationRoles.Contains(normalizedRole);
        }

        if (path.StartsWith("/api/routes", StringComparison.OrdinalIgnoreCase))
        {
            return AllApplicationRoles.Contains(normalizedRole);
        }

        if (path.StartsWith("/api/route-optimization-runs", StringComparison.OrdinalIgnoreCase))
        {
            return isMutation
                ? normalizedRole is AppUserRoles.Logistica or AppUserRoles.Admin or AppUserRoles.AdminSystem
                : AllApplicationRoles.Contains(normalizedRole);
        }

        if (path.StartsWith("/api/production", StringComparison.OrdinalIgnoreCase))
        {
            return LogisticsRoles.Contains(normalizedRole);
        }

        if (StartsWithAny(
            path,
            "/api/logistics/map",
            "/api/customers",
            "/api/fiscal-documents",
            "/api/products",
            "/api/inventory"))
        {
            return AllApplicationRoles.Contains(normalizedRole);
        }

        return AllApplicationRoles.Contains(normalizedRole);
    }

    private static bool StartsWithAny(string path, params string[] prefixes) =>
        prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
