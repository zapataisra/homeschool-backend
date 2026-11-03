using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Añade los claims <c>family_id</c> y <c>role</c> a la cookie de sesión de un adulto.
/// </summary>
public sealed class FamilyMemberClaimsPrincipalFactory : UserClaimsPrincipalFactory<FamilyMember>
{
    public FamilyMemberClaimsPrincipalFactory(
        UserManager<FamilyMember> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(FamilyMember user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(ClaimNames.FamilyId, user.FamilyId.ToString()));
        identity.AddClaim(new Claim(ClaimNames.Role, user.Role.ToString()));

        return identity;
    }
}
