using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using HomeschoolAI.Domain.Auth;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Implementación de <see cref="IUserContextProvider"/>: lee el <see cref="UserContext"/>
/// desde los claims del <see cref="HttpContext"/> autenticado.
/// </summary>
public sealed class HttpUserContextProvider : IUserContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpUserContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<UserContext?> GetAsync(CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult<UserContext?>(null);
        }

        var memberId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var familyIdRaw = principal.FindFirstValue(ClaimNames.FamilyId);
        var roleRaw = principal.FindFirstValue(ClaimNames.Role);
        var childProfileIdRaw = principal.FindFirstValue(ClaimNames.ChildProfileId);
        var childName = principal.FindFirstValue(ClaimNames.ChildName);

        if (memberId is null || familyIdRaw is null || roleRaw is null || !Guid.TryParse(familyIdRaw, out var familyId))
        {
            return Task.FromResult<UserContext?>(null);
        }

        var role = roleRaw switch
        {
            nameof(UserRole.Owner) => UserRole.Owner,
            nameof(UserRole.Guardian) => UserRole.Guardian,
            nameof(UserRole.Child) => UserRole.Child,
            _ => (UserRole?)null
        };

        if (role is null)
        {
            return Task.FromResult<UserContext?>(null);
        }

        var childProfileId = Guid.TryParse(childProfileIdRaw, out var cpid) ? cpid : (Guid?)null;

        return Task.FromResult<UserContext?>(new UserContext(memberId, familyId, role.Value, childProfileId, childName));
    }
}
