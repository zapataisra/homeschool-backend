namespace HomeschoolAI.Domain.Auth;

/// <summary>
/// Puerto de salida: obtiene el <see cref="UserContext"/> del usuario autenticado
/// actual (null si no hay sesión). La implementación lee los claims del HttpContext.
/// </summary>
public interface IUserContextProvider
{
    Task<UserContext?> GetAsync(CancellationToken cancellationToken = default);
}
