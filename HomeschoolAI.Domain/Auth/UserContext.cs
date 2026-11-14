namespace HomeschoolAI.Domain.Auth;

/// <summary>
/// Identidad del usuario autenticado, derivada de los claims de la cookie.
/// Toda consulta de datos de negocio debe filtrarse por <see cref="FamilyId"/>.
/// </summary>
public sealed record UserContext(
    string FamilyMemberId,
    Guid FamilyId,
    UserRole Role,
    Guid? ChildProfileId = null,
    string? ChildName = null);
