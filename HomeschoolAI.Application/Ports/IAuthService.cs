using HomeschoolAI.Application.Auth;

namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Puerto de entrada: autenticación (registro, login, logout) y sesión delegada del niño.
/// La implementación usa ASP.NET Core Identity (SignInManager) y vive en Adapters,
/// porque Application no puede referenciar ASP.NET (regla §4.1).
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>Inicia la sesión delegada del niño (el adulto selecciona un ChildProfile de su familia).</summary>
    Task<ChildSessionResponse> StartChildSessionAsync(Guid childId, CancellationToken cancellationToken = default);
}
