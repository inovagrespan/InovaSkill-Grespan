using Microsoft.AspNetCore.Mvc;

namespace InovaSkill.Importer.Api.Auth;

public sealed class JwtAuthMiddleware(RequestDelegate next)
{
    private static readonly PathString[] PublicPaths = ["/login", "/register"];

    public async Task InvokeAsync(HttpContext context, JwtTokenService tokenService)
    {
        if (PublicPaths.Any(path => context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        const string bearerPrefix = "Bearer ";
        var header = context.Request.Headers.Authorization.ToString();
        var token = header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? header[bearerPrefix.Length..].Trim()
            : ResolveHubAccessToken(context);

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Não autenticado",
                Detail = "Faça login para acessar este recurso.",
                Status = StatusCodes.Status401Unauthorized
            });
            return;
        }

        var principal = tokenService.Validate(token);
        if (principal is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Token inválido",
                Detail = "A sessão é inválida ou expirou. Faça login novamente.",
                Status = StatusCodes.Status401Unauthorized
            });
            return;
        }

        context.User = principal;
        await next(context);
    }

    private static string? ResolveHubAccessToken(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/hubs"))
        {
            return null;
        }

        return context.Request.Query.TryGetValue("access_token", out var accessToken)
            ? accessToken.ToString()
            : null;
    }
}
