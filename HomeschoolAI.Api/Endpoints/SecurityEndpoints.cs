using Microsoft.AspNetCore.Antiforgery;

namespace HomeschoolAI.Api.Endpoints;

/// <summary>
/// Endpoint que emite el token anti-CSRF (y su cookie HttpOnly) para que el frontend
/// lo envíe en la cabecera X-CSRF-TOKEN en las futuras mutaciones.
/// </summary>
public static class SecurityEndpoints
{
    public static void MapSecurityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/antiforgery/token", (HttpContext httpContext, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(httpContext);
                return Results.Ok(new { token = tokens.RequestToken });
            })
            .WithName("GetAntiforgeryToken");
    }
}
