using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Learning;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Adapters.Learning;

/// <summary>
/// Persiste una actualización de progreso pendiente de forma idempotente:
/// UPSERT de <c>child_topic_progress</c> por (child_profile_id, topic_id) y de
/// <c>child_progress</c> con XP monotónica (GREATEST) para no duplicar XP.
/// </summary>
public sealed class ProgressSyncService : IProgressSyncService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProgressSyncService> _logger;

    public ProgressSyncService(AppDbContext db, ILogger<ProgressSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> SyncAsync(PendingProgressUpdate update, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = update.NewStatus.ToString();
            var completedAt = update.NewStatus == TopicStatus.Completed ? update.AttemptedAtUtc : (DateTime?)null;
            var currentLevel = ChildProgress.CalculateLevel(update.TotalXp);

            // Transacción: el tema y la XP se persisten juntos (todo o nada) para que
            // un reintento nunca deje el progreso a medias.
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            // 1) UPSERT del tema (idempotente: reemplaza estado/intentos/insignia).
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO app_identity.child_topic_progress
                    (id, child_profile_id, topic_id, topic_title, campo_formativo, status, attempts,
                     time_spent_seconds, badge_earned, started_at_utc, last_activity_at_utc, completed_at_utc)
                VALUES
                    ({Guid.NewGuid()}, {update.ChildProfileId}, {update.TopicId}, {update.TopicTitle},
                     {update.CampoFormativo}, {status}, {update.Attempts}, 0, {update.BadgeEarned},
                     {update.AttemptedAtUtc}, {update.AttemptedAtUtc}, {completedAt})
                ON CONFLICT (child_profile_id, topic_id) DO UPDATE SET
                    status = EXCLUDED.status,
                    attempts = EXCLUDED.attempts,
                    badge_earned = EXCLUDED.badge_earned,
                    last_activity_at_utc = EXCLUDED.last_activity_at_utc,
                    completed_at_utc = EXCLUDED.completed_at_utc
                """, cancellationToken);

            // 2) UPSERT de la XP acumulada (monotónica: nunca retrocede al reintentar).
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO app_identity.child_progress (child_profile_id, total_xp, current_level)
                VALUES ({update.ChildProfileId}, {update.TotalXp}, {currentLevel})
                ON CONFLICT (child_profile_id) DO UPDATE SET
                    total_xp = GREATEST(app_identity.child_progress.total_xp, EXCLUDED.total_xp),
                    current_level = GREATEST(app_identity.child_progress.total_xp, EXCLUDED.total_xp) / 100 + 1
                """, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo persistir el progreso pendiente del niño {ChildId}, tema {TopicId}.", update.ChildProfileId, update.TopicId);
            return false;
        }
    }
}