namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Rol de un adulto dentro de su familia (qué es dentro del tenant).
/// El rol <c>Admin</c> es de plataforma y NO pertenece a una Family.
/// </summary>
public enum FamilyRole
{
    Owner = 0,
    Guardian = 1
}
