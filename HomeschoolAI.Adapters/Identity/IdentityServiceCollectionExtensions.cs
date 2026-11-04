using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Adapters.Email;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Auth;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Registro de dependencias del contexto de identidad y accesos (semana 3).
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="AppDbContext"/> (esquema <c>app_identity</c>) y los servicios
    /// de ASP.NET Core Identity para el usuario <see cref="FamilyMember"/>.
    /// </summary>
    public static IServiceCollection AddHomeschoolIdentity(
        this IServiceCollection services,
        string connectionString)
    {
        // Contexto de escritura. No usa UseVector(): en este esquema no hay columnas vector.
        services.AddDbContext<AppDbContext>(dbOptions =>
            dbOptions.UseNpgsql(connectionString));

        // Identity para el usuario FamilyMember.
        services.AddIdentityCore<FamilyMember>(identity =>
            {
                identity.Password.RequiredLength = 8;
                identity.Password.RequireDigit = true;
                identity.Password.RequireUppercase = true;
                identity.Password.RequireNonAlphanumeric = false;

                identity.User.RequireUniqueEmail = true;

                // Bloqueo por intentos fallidos (OWASP: fuerza bruta y relleno de credenciales).
                identity.Lockout.AllowedForNewUsers = true;
                identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                identity.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddSignInManager();

        // Registro del envío de correo para Identity (confirmación/recuperación).
        services.AddScoped<IEmailSender<FamilyMember>, IdentityEmailSender>();

        // ADR (semana 3): cookies (HttpOnly, Secure, SameSite) para la PWA.
        // Al ser una API REST, las redirecciones devuelven 401/403 en lugar de vistas HTML.
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        // Contexto del usuario autenticado (claims de la cookie).
        services.AddHttpContextAccessor();
        services.AddScoped<IUserClaimsPrincipalFactory<FamilyMember>, FamilyMemberClaimsPrincipalFactory>();
        services.AddScoped<IUserContextProvider, HttpUserContextProvider>();

        // Servicios de autenticación y de familia (aislamiento por FamilyId).
        services.AddScoped<IAuthService, IdentityAuthService>();
        services.AddScoped<IFamilyService, IdentityFamilyService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IInteractionQuotaService, InteractionQuotaService>();

        // Políticas de autorización por rol (Owner, Guardian, Child).
        services.AddHomeschoolAuthorization();

        return services;
    }
}
