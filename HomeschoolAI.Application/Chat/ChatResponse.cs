using HomeschoolAI.Application.Learning;

namespace HomeschoolAI.Application.Chat;

/// <summary>
/// Respuesta del agente: el hilo de conversación (para continuar el diálogo) y la respuesta.
/// En la sesión de niño, incluye el desenlace del intento, las métricas actualizadas y,
/// si el tema se cerró, la siguiente lección lista para mostrarse sin recargar la página.
/// </summary>
public sealed record ChatResponse(
    string ThreadId,
    string Content,
    AttemptOutcomeDto? AttemptOutcome = null,
    LearningMetricsDto? Metrics = null,
    ActiveLessonDto? NextLesson = null);
