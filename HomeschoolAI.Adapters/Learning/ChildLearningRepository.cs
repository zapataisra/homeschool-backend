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
    private readonly ILogger<ChildLearningRepository> _logger;

    public ChildLearningRepository(AppDbContext db, ILogger<ChildLearningRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LearningSnapshot> GetOrCreateActiveLessonAsync(
        Guid childProfileId,
        Guid familyId,
        DateTime nowUtc,
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

        var active = await _db.ChildTopicProgresses
            .SingleOrDefaultAsync(t => t.ChildProfileId == childProfileId && t.Status == TopicStatus.InProgress, cancellationToken);

        if (active is null)
        {
            var usedTopicIds = await _db.ChildTopicProgresses
                .Where(t => t.ChildProfileId == childProfileId)
                .Select(t => t.TopicId)
                .ToListAsync(cancellationToken);

            var next = SeedCurriculumCatalog.SelectNextTopic(child.Grade, usedTopicIds);

            active = new ChildTopicProgress
            {
                Id = Guid.NewGuid(),
                ChildProfileId = childProfileId,
                TopicId = next.TopicId,
                TopicTitle = next.Title,
                CampoFormativo = next.CampoFormativo,
                Status = TopicStatus.InProgress,
                Attempts = 0,
                TimeSpentSeconds = 0,
                StartedAtUtc = nowUtc,
                LastActivityAtUtc = nowUtc
            };
            _db.ChildTopicProgresses.Add(active);
        }
        else
        {
            active.RecordActivity(nowUtc);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var topics = await _db.ChildTopicProgresses.AsNoTracking()
            .Where(t => t.ChildProfileId == childProfileId)
            .OrderByDescending(t => t.LastActivityAtUtc)
            .ToListAsync(cancellationToken);

        var history = topics
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

        var seed = SeedCurriculumCatalog.Find(active.TopicId);

        LogLessonSelected(_logger, childProfileId, active.TopicId, history.Count);

        return new LearningSnapshot(
            new ExplorerSummaryDto(child.Name, child.Grade, child.Fase, child.AvatarUrl),
            progress.CurrentLevel,
            progress.TotalXP,
            history,
            SeedCurriculumCatalog.ToActiveLesson(seed),
            seed.SearchQuery);
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

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information,
        Message = "Lección activa seleccionada: niño {ChildProfileId}, tema {TopicId}, temas en historial {HistoryCount}")]
    private static partial void LogLessonSelected(ILogger logger, Guid childProfileId, string topicId, int historyCount);
}