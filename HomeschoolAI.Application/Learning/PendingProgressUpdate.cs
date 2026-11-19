using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Application.Learning;

/// <summary>
/// Actualización de progreso pendiente de persistir cuando PostgreSQL no está disponible.
/// Se persiste de forma idempotente: UPSERT por (ChildProfileId, TopicId) y XP acumulada
/// monotónica (GREATEST) para no duplicar XP al reintentar.
/// </summary>
public sealed record PendingProgressUpdate(
    Guid ChildProfileId,
    string TopicId,
    string TopicTitle,
    string CampoFormativo,
    TopicStatus NewStatus,
    int Attempts,
    int XpAwarded,
    string? BadgeEarned,
    int TotalXp,
    DateTime AttemptedAtUtc);