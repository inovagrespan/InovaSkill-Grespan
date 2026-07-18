using System.Security.Claims;

namespace InovaSkill.Importer.Api.Auth;

public sealed class ApiAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role)
            ?? context.User.FindFirstValue("role");

        if (!ApiAccessPolicy.CanAccess(
            role,
            context.Request.Path.Value ?? string.Empty,
            context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Acesso negado",
                detail = "Seu perfil não possui permissão para acessar este recurso."
            });
            return;
        }

        await next(context);
    }
}
