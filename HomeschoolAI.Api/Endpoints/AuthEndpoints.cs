using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using HomeschoolAI.Application.Auth;
using HomeschoolAI.Application.Ports;

namespace HomeschoolAI.Api.Endpoints;

/// <summary>Endpoints de autenticación: registro, login y logout.</summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/register", async (
                RegisterRequest request,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var result = await authService.RegisterAsync(request, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("Register")
            .RequireRateLimiting("auth");

        endpoints.MapPost("/api/auth/login", async (
                LoginRequest request,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var result = await authService.LoginAsync(request, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("Login")
            .RequireRateLimiting("auth");

        endpoints.MapPost("/api/auth/logout", async (
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                await authService.LogoutAsync(cancellationToken);
                return Results.NoContent();
            })
            .WithName("Logout")
            .RequireAuthorization();
    }
}
