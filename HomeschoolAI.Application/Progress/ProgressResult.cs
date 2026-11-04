namespace HomeschoolAI.Application.Progress;

/// <summary>
/// Resultado de sumar XP: progreso acumulado del niño y si hubo subida de nivel.
/// Se devuelve como DTO para no exponer la entidad de persistencia.
/// </summary>
public sealed record ProgressResult(
    Guid ChildProfileId,
    int TotalXP,
    int CurrentLevel,
    bool LeveledUp);
