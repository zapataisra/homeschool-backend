using Microsoft.AspNetCore.Identity;

namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Adulto con cuenta de acceso (Owner o Guardian). Hereda de <see cref="IdentityUser"/>
/// para reutilizar credenciales, hash de contraseñas, bloqueo y 2FA de ASP.NET Core Identity.
/// </summary>
public sealed class FamilyMember : IdentityUser
{
    public Guid FamilyId { get; set; }

    public FamilyRole Role { get; set; }

    public Family? Family { get; set; }
}
