using HomeschoolAI.Application.Learning;

namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Puerto de salida: persistencia del progreso de aprendizaje del niño (temas, tiempo y XP).
/// La implementación vive en Adapters (AppDbContext); Application no conoce EF Core.
/// </summary>
public interface IChildLearningRepository
{
    /// <summary>
    /// Obtiene (o crea) el tema en curso del niño y devuelve el snapshot de aprendizaje,
    /// registrando actividad sobre ese tema.
    /// </summary>
    Task<LearningSnapshot> GetOrCreateActiveLessonAsync(
        Guid childProfileId,
        Guid familyId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Registra actividad sobre el tema InProgress del niño (acumula tiempo). No-op si no hay tema.</summary>
    Task RecordActivityAsync(Guid childProfileId, Guid familyId, DateTime nowUtc, CancellationToken cancellationToken = default);
}