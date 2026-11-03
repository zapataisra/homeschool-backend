using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using HomeschoolAI.Domain.Auth;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Políticas de autorización por rol. La autorización se verifica SIEMPRE en código
/// (nunca la decide el LLM) y se complementa con el filtrado por FamilyId en los servicios.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Adulto de la familia (Owner o Guardian).</summary>
    public const string RequireFamilyMember = "RequireFamilyMember";

    /// <summary>Solo Owner (facturación, invitaciones, exportación).</summary>
    public const string RequireOwner = "RequireOwner";

    /// <summary>Sesión delegada del niño.</summary>
    public const string RequireChild = "RequireChild";

    public static IServiceCollection AddHomeschoolAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RequireFamilyMember, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireClaim(ClaimNames.FamilyId)
                    .RequireClaim(ClaimNames.Role, nameof(UserRole.Owner), nameof(UserRole.Guardian)));

            options.AddPolicy(RequireOwner, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireClaim(ClaimNames.FamilyId)
                    .RequireClaim(ClaimNames.Role, nameof(UserRole.Owner)));

            options.AddPolicy(RequireChild, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireClaim(ClaimNames.Role, nameof(UserRole.Child))
                    .RequireClaim(ClaimNames.ChildProfileId));
        });

        return services;
    }
}
