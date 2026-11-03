namespace HomeschoolAI.Application.Auth;

/// <summary>Registro de un adulto (crea una Family y un FamilyMember Owner).</summary>
public sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FamilyName { get; init; } = string.Empty;
}

/// <summary>Inicio de sesión del adulto (Owner/Guardian).</summary>
public sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

/// <summary>Resultado de registro/login.</summary>
public sealed record AuthResponse(string FamilyMemberId, Guid FamilyId, string Role);

/// <summary>Petición para crear un perfil de niño.</summary>
public sealed class CreateChildRequest
{
    public string Name { get; init; } = string.Empty;

    public int Grade { get; init; }

    public int? Fase { get; init; }
}
