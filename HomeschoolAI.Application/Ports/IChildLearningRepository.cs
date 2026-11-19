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
    /// registrando actividad sobre ese tema. Si no hay un tema InProgress y
    /// <paramref name="selectedTopicId"/> es <c>null</c>, no inserta en BD y devuelve
    /// <c>ActiveLesson = null</c> junto con las misiones disponibles.
    /// </summary>
    Task<LearningSnapshot> GetOrCreateActiveLessonAsync(
        Guid childProfileId,
        Guid familyId,
        DateTime nowUtc,
        string? selectedTopicId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve las 3 misiones del grado del niño, marcando como recomendada la que
    /// sugiere el selector curricular (trimestre SEP actual o prerrequisito).
    /// </summary>
    Task<IReadOnlyList<MissionOptionDto>> GetMissionOptionsAsync(
        Guid familyId,
        Guid childProfileId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Registra actividad sobre el tema InProgress del niño (acumula tiempo). No-op si no hay tema.</summary>
    Task RecordActivityAsync(Guid childProfileId, Guid familyId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el tema en curso (InProgress) del niño, con su consulta curricular, o
    /// <c>null</c> si no hay ninguno. Verifica la pertenencia del niño a la familia (Cero IDOR).
    /// </summary>
    Task<ActiveTopicDto?> GetActiveTopicAsync(Guid familyId, Guid childProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica el resultado de un intento de evaluación sobre un tema: verifica la
    /// pertenencia del niño a la familia (Cero IDOR), avanza la máquina de estados,
    /// suma la XP y devuelve el desenlace junto con las métricas actualizadas.
    /// </summary>
    Task<AttemptOutcomeDto> ApplyAttemptOutcomeAsync(
        Guid familyId,
        Guid childProfileId,
        string topicId,
        bool isCorrect,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}