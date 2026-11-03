namespace HomeschoolAI.Application.Auth;

/// <summary>Información de la familia del usuario autenticado.</summary>
public sealed record FamilyInfoResponse(Guid FamilyId, string Name, int MemberCount, int ChildCount);

/// <summary>Perfil de un niño (sin credenciales).</summary>
public sealed record ChildProfileDto(Guid Id, string Name, int Grade, int? Fase);

/// <summary>Respuesta al iniciar la sesión delegada de un niño.</summary>
public sealed record ChildSessionResponse(Guid ChildProfileId, string ChildName);

/// <summary>Placeholder del panel de facturación (solo Owner).</summary>
public sealed record BillingResponse(Guid FamilyId, string Status);
