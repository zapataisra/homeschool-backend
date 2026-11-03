namespace HomeschoolAI.Domain.Auth;

/// <summary>
/// Rol de un usuario en el contexto de autorización (qué es).
/// <c>Child</c> representa la sesión delegada del niño (sin credenciales propias).
/// </summary>
public enum UserRole
{
    Owner = 0,
    Guardian = 1,
    Child = 2
}
