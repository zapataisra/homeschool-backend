using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Application.Learning;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Domain.Exceptions;
using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Adapters.Learning;

/// <summary>
/// Implementación de <see cref="IChildLearningRepository"/> sobre <see cref="AppDbContext"/>.
/// Toda consulta se filtra por el par (ChildProfileId, FamilyId) del contexto autenticado
/// (Cero IDOR).
/// </summary>
public sealed partial class ChildLearningRepository : IChildLearningRepository
{
    private readonly AppDbContext _db;
    private readonly ICurriculumCatalog _curriculumCatalog;
    private readonly IPendingProgressSyncQueue _pendingSyncQueue;
    private readonly ILogger<ChildLearningRepository> _logger;

    public ChildLearningRepository(
        AppDbContext db,
        ICurriculumCatalog curriculumCatalog,
        IPendingProgressSyncQueue pendingSyncQueue,
        ILogger<ChildLearningRepository> logger)
    {
        _db = db;
        _curriculumCatalog = curriculumCatalog;
        _pendingSyncQueue = pendingSyncQueue;
        _logger = logger;
    }

    public async Task<LearningSnapshot> GetOrCreateActiveLessonAsync(
        Guid childProfileId,
        Guid familyId,
        DateTime nowUtc,
        string? selectedTopicId = null,
        CancellationToken cancellationToken = default)
    {
        var child = await _db.ChildProfiles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == childProfileId && c.FamilyId == familyId, cancellationToken)
            ?? throw new ResourceNotFoundException("ChildProfile");

        var progress = await _db.ChildProgress
            .SingleOrDefaultAsync(p => p.ChildProfileId == childProfileId, cancellationToken);
        if (progress is null)
        {
            progress = new ChildProgress { ChildProfileId = childProfileId, TotalXP = 0, CurrentLevel = 1 };
            _db.ChildProgress.Add(progress);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var allTopics = await _db.ChildTopicProgresses
            .Where(t => t.ChildProfileId == childProfileId)
            .ToListAsync(cancellationToken);

        var isNewDayReturn = false;
        var active = allTopics.FirstOrDefault(t => t.Status == TopicStatus.InProgress);

        if (active is null)
        {
            if (string.IsNullOrWhiteSpace(selectedTopicId))
            {
                // Modo selección de misión: no se crea tema ni se gasta cuota/LLM.
                var missions = await GetMissionOptionsAsync(familyId, childProfileId, nowUtc, cancellationToken);
                return new LearningSnapshot(
                    new ExplorerSummaryDto(child.Name, child.Grade, child.Fase, child.AvatarUrl),
                    progress.CurrentLevel,
                    progress.TotalXP,
                    MapHistory(allTopics),
                    ActiveLesson: null,
                    missions,
                    SearchQuery: string.Empty);
            }

            // El alumno eligió una misión concreta: crear o reactivar el tema elegido.
            var seed = await _curriculumCatalog.FindAsync(selectedTopicId, cancellationToken)
                ?? throw new InvalidOperationException($"No se encontró el tema '{selectedTopicId}' en el catálogo curricular.");
            active = allTopics.FirstOrDefault(t => t.TopicId == selectedTopicId);
            if (active is null)
            {
                active = new ChildTopicProgress
                {
                    Id = Guid.NewGuid(),
                    ChildProfileId = childProfileId,
                    TopicId = seed.TopicId,
                    TopicTitle = seed.Title,
                    CampoFormativo = seed.CampoFormativo,
                    Status = TopicStatus.InProgress,
                    Attempts = 0,
                    TimeSpentSeconds = 0,
                    StartedAtUtc = nowUtc,
                    LastActivityAtUtc = nowUtc
                };
                _db.ChildTopicProgresses.Add(active);
            }
            else if (active.Status == TopicStatus.Partial)
            {
                // Reintento tras completar el prerrequisito: reinicia los intentos del tema pausado.
                active.Status = TopicStatus.InProgress;
                active.Attempts = 0;
                active.BadgeEarned = null;
                active.CompletedAtUtc = null;
                active.LastActivityAtUtc = nowUtc;
            }
            else
            {
                active.RecordActivity(nowUtc);
            }
        }
        else
        {
            // Vuelve al mismo tema en curso: detecta si es un regreso en un día distinto.
            isNewDayReturn = active.LastActivityAtUtc.Date < nowUtc.Date;
            active.RecordActivity(nowUtc);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var topics = await _db.ChildTopicProgresses.AsNoTracking()
            .Where(t => t.ChildProfileId == childProfileId)
            .OrderByDescending(t => t.LastActivityAtUtc)
            .ToListAsync(cancellationToken);

        var history = MapHistory(topics);

        var seedForLesson = await _curriculumCatalog.FindAsync(active.TopicId, cancellationToken)
            ?? throw new InvalidOperationException($"No se encontró el tema '{active.TopicId}' en el catálogo curricular.");

        LogLessonSelected(_logger, childProfileId, active.TopicId, history.Count);

        return new LearningSnapshot(
            new ExplorerSummaryDto(child.Name, child.Grade, child.Fase, child.AvatarUrl),
            progress.CurrentLevel,
            progress.TotalXP,
            history,
            SeedCurriculumCatalog.ToActiveLesson(seedForLesson),
            AvailableMissions: null,
            seedForLesson.SearchQuery,
            IsNewDayReturn: isNewDayReturn);
    }

    public async Task RecordActivityAsync(Guid childProfileId, Guid familyId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // Cero IDOR: se verifica la pertenencia del niño a la familia antes de tocar su progreso.
        var belongs = await _db.ChildProfiles
            .AnyAsync(c => c.Id == childProfileId && c.FamilyId == familyId, cancellationToken);
        if (!belongs)
        {
            return;
        }

        var active = await _db.ChildTopicProgresses
            .SingleOrDefaultAsync(t => t.ChildProfileId == childProfileId && t.Status == TopicStatus.InProgress, cancellationToken);
        if (active is null)
        {
            return;
        }

        active.RecordActivity(nowUtc);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ActiveTopicDto?> GetActiveTopicAsync(
        Guid familyId,
        Guid childProfileId,
        CancellationToken cancellationToken = default)
    {
        // Cero IDOR: el niño debe pertenecer a la familia autenticada.
        var child = await _db.ChildProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == childProfileId && c.FamilyId == familyId, cancellationToken)
            ?? throw new ResourceNotFoundException("ChildProfile");

        var active = await _db.ChildTopicProgresses
            .SingleOrDefaultAsync(t => t.ChildProfileId == childProfileId && t.Status == TopicStatus.InProgress, cancellationToken);
        if (active is null)
        {
            return null;
        }

        var seed = await _curriculumCatalog.FindAsync(active.TopicId, cancellationToken)
            ?? throw new InvalidOperationException($"No se encontró el tema '{active.TopicId}' en el catálogo curricular.");
        return new ActiveTopicDto(active.TopicId, active.TopicTitle, active.CampoFormativo, seed.SearchQuery, active.Attempts, child.Grade, seed.WeeklyProjectGoal);
    }

    public async Task<IReadOnlyList<MissionOptionDto>> GetMissionOptionsAsync(
        Guid familyId,
        Guid childProfileId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var child = await _db.ChildProfiles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == childProfileId && c.FamilyId == familyId, cancellationToken)
            ?? throw new ResourceNotFoundException("ChildProfile");

        var allTopics = await _db.ChildTopicProgresses.AsNoTracking()
            .Where(t => t.ChildProfileId == childProfileId)
            .ToListAsync(cancellationToken);

        // Rotación: solo temas pendientes (no completados ni pausados); InProgress primero.
        var gradeTopics = await _curriculumCatalog.GetTopicsForGradeAsync(child.Grade, cancellationToken);
        var recommended = SeedCurriculumCatalog.SelectNextTopic(gradeTopics, nowUtc, allTopics);
        var options = SeedCurriculumCatalog.SelectMissionOptions(gradeTopics, allTopics, limit: 3);

        return options
            .Select(topic => new MissionOptionDto(
                topic.TopicId,
                topic.Title,
                topic.CampoFormativo,
                topic.WeeklyProjectTitle,
                topic.WeeklyProjectGoal,
                topic.Trimester,
                topic.OrderIndex,
                IsRecommended: topic.TopicId == recommended.TopicId,
                Status: StatusOf(allTopics, topic.TopicId)))
            .ToList();
    }

    private static string? StatusOf(IReadOnlyList<ChildTopicProgress> topics, string topicId)
    {
        var topic = topics.FirstOrDefault(t => t.TopicId == topicId);
        return topic?.Status switch
        {
            TopicStatus.Completed => nameof(TopicStatus.Completed),
            TopicStatus.Partial => nameof(TopicStatus.Partial),
            _ => null
        };
    }

    public async Task<AttemptOutcomeDto> ApplyAttemptOutcomeAsync(
        Guid familyId,
        Guid childProfileId,
        string topicId,
        bool isCorrect,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        // Cero IDOR: el niño debe pertenecer a la familia autenticada.
        var belongs = await _db.ChildProfiles
            .AnyAsync(c => c.Id == childProfileId && c.FamilyId == familyId, cancellationToken);
        if (!belongs)
        {
            throw new ResourceNotFoundException("ChildProfile");
        }

        var topic = await _db.ChildTopicProgresses
            .SingleOrDefaultAsync(t => t.ChildProfileId == childProfileId && t.TopicId == topicId, cancellationToken);

        topic ??= await _db.ChildTopicProgresses
            .SingleOrDefaultAsync(t => t.ChildProfileId == childProfileId && t.Status == TopicStatus.InProgress, cancellationToken);

        if (topic is null)
        {
            throw new ResourceNotFoundException("ChildTopicProgress");
        }

        var outcome = topic.RegisterAttempt(isCorrect, nowUtc);

        var progress = await _db.ChildProgress
            .SingleOrDefaultAsync(p => p.ChildProfileId == childProfileId, cancellationToken);
        if (progress is null)
        {
            progress = new ChildProgress { ChildProfileId = childProfileId, TotalXP = 0, CurrentLevel = 1 };
            _db.ChildProgress.Add(progress);
        }

        progress.TotalXP += outcome.XpAwarded;
        progress.CurrentLevel = ChildProgress.CalculateLevel(progress.TotalXP);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // PostgreSQL no disponible: encolar la actualización para reconciliarla cuando
            // vuelva la BD. El UPSERT es idempotente por (ChildProfileId, TopicId) y la XP
            // acumulada es monotónica (GREATEST), de modo que los reintentos no duplican XP.
            _logger.LogWarning(ex, "No se pudo persistir el intento del niño {ChildId} en {TopicId}; se encola para reconciliación.", childProfileId, topic.TopicId);
            _pendingSyncQueue.Enqueue(new PendingProgressUpdate(
                childProfileId,
                topic.TopicId,
                topic.TopicTitle,
                topic.CampoFormativo,
                outcome.NewStatus,
                outcome.AttemptNumber,
                outcome.XpAwarded,
                outcome.BadgeEarned,
                progress.TotalXP,
                nowUtc));

            // Degradado: devolver el desenlace calculado en memoria sin historial
            // (la BD sigue caída); el frontend no ve un error técnico.
            return new AttemptOutcomeDto(
                outcome,
                LearningMetricsFactory.Build(progress.CurrentLevel, progress.TotalXP, Array.Empty<TopicHistoryItemDto>()));
        }

        var topics = await _db.ChildTopicProgresses.AsNoTracking()
            .Where(t => t.ChildProfileId == childProfileId)
            .OrderByDescending(t => t.LastActivityAtUtc)
            .ToListAsync(cancellationToken);

        var history = MapHistory(topics);
        var metrics = LearningMetricsFactory.Build(progress.CurrentLevel, progress.TotalXP, history);

        LogAttemptApplied(_logger, childProfileId, topic.TopicId, outcome.NewStatus.ToString(), outcome.XpAwarded);

        return new AttemptOutcomeDto(outcome, metrics);
    }

    private static List<TopicHistoryItemDto> MapHistory(IEnumerable<ChildTopicProgress> topics) =>
        topics
            .Select(t => new TopicHistoryItemDto(
                t.TopicId,
                t.TopicTitle,
                t.CampoFormativo,
                t.Status.ToString(),
                t.Attempts,
                t.TimeSpentSeconds,
                t.BadgeEarned,
                t.LastActivityAtUtc))
            .ToList();

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information,
        Message = "Lección activa seleccionada: niño {ChildProfileId}, tema {TopicId}, temas en historial {HistoryCount}")]
    private static partial void LogLessonSelected(ILogger logger, Guid childProfileId, string topicId, int historyCount);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Information,
        Message = "Intento aplicado: niño {ChildProfileId}, tema {TopicId}, nuevo estado {Status}, XP otorgada {XpAwarded}")]
    private static partial void LogAttemptApplied(ILogger logger, Guid childProfileId, string topicId, string status, int xpAwarded);
}