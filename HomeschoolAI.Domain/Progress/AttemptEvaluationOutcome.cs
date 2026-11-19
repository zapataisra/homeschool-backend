namespace HomeschoolAI.Domain.Progress;

/// <summary>
/// Resultado de registrar un intento de evaluación sobre un tema: nuevo estado,
/// número de intento, XP otorgada, insignia obtenida (si la hay) y si el tema
/// queda cerrado (no admite más intentos).
/// </summary>
public sealed record AttemptEvaluationOutcome(
    TopicStatus NewStatus,
    int AttemptNumber,
    int XpAwarded,
    string? BadgeEarned,
    bool IsTopicClosed);
