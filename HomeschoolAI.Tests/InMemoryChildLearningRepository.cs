using HomeschoolAI.Adapters.Learning;
using HomeschoolAI.Application.Learning;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Exceptions;
using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Tests;

/// <summary>
/// Doble en memoria de <see cref="IChildLearningRepository"/> para las pruebas del
/// bucle de entendimiento. Reutiliza la lógica real de dominio (máquina de estados,
/// regla de nivel y selector curricular) sin tocar la base de datos.
/// </summary>
public sealed class InMemoryChildLearningRepository : IChildLearningRepository
{
    private readonly List<ChildTopicProgress> _topics = new();
    private readonly ChildProgress _progress = new()
    {
        ChildProfileId = Guid.Empty,
        TotalXP = 0,
        CurrentLevel = 1
    };

    public int Grade { get; init; } = 4;

    public IReadOnlyList<ChildTopicProgress> Topics => _topics;

    public ChildProgress Progress => _progress;

    public void Seed(string topicId, TopicStatus status, int attempts = 0, DateTime? lastActivityUtc = null)
    {
        var now = lastActivityUtc ?? new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);
        _topics.Add(new ChildTopicProgress
        {
            Id = Guid.NewGuid(),
            ChildProfileId = Guid.NewGuid(),
            TopicId = topicId,
            TopicTitle = topicId,
            CampoFormativo = "Saberes y Pensamiento Científico",
            Status = status,
            Attempts = attempts,
            StartedAtUtc = now,
            LastActivityAtUtc = now
        });
    }

    public Task<ActiveTopicDto?> GetActiveTopicAsync(Guid familyId, Guid childProfileId, CancellationToken cancellationToken = default)
    {
        var active = _topics.FirstOrDefault(t => t.Status == TopicStatus.InProgress);
        if (active is null)
        {
            return Task.FromResult<ActiveTopicDto?>(null);
        }

        var seed = SeedCurriculumCatalog.Find(active.TopicId);
        return Task.FromResult<ActiveTopicDto?>(new ActiveTopicDto(active.TopicId, active.TopicTitle, active.CampoFormativo, seed.SearchQuery, active.Attempts, Grade, seed.WeeklyProjectGoal));
    }

    public Task<AttemptOutcomeDto> ApplyAttemptOutcomeAsync(Guid familyId, Guid childProfileId, string topicId, bool isCorrect, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var topic = _topics.FirstOrDefault(t => t.TopicId == topicId)
            ?? _topics.FirstOrDefault(t => t.Status == TopicStatus.InProgress)
            ?? throw new ResourceNotFoundException("ChildTopicProgress");

        var outcome = topic.RegisterAttempt(isCorrect, nowUtc);

        _progress.TotalXP += outcome.XpAwarded;
        _progress.CurrentLevel = ChildProgress.CalculateLevel(_progress.TotalXP);

        var metrics = LearningMetricsFactory.Build(_progress.CurrentLevel, _progress.TotalXP, MapHistory());
        return Task.FromResult(new AttemptOutcomeDto(outcome, metrics));
    }

    public Task<LearningSnapshot> GetOrCreateActiveLessonAsync(Guid childProfileId, Guid familyId, DateTime nowUtc, string? selectedTopicId = null, CancellationToken cancellationToken = default)
    {
        var active = _topics.FirstOrDefault(t => t.Status == TopicStatus.InProgress);

        if (active is null)
        {
            if (string.IsNullOrWhiteSpace(selectedTopicId))
            {
                return Task.FromResult(new LearningSnapshot(
                    new ExplorerSummaryDto("Mía", Grade, null, "avatar-aguila.svg"),
                    _progress.CurrentLevel,
                    _progress.TotalXP,
                    MapHistory(),
                    ActiveLesson: null,
                    BuildMissionOptions(nowUtc),
                    SearchQuery: string.Empty));
            }

            var seed = SeedCurriculumCatalog.Find(selectedTopicId);
            active = _topics.FirstOrDefault(t => t.TopicId == selectedTopicId);
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
                    StartedAtUtc = nowUtc,
                    LastActivityAtUtc = nowUtc
                };
                _topics.Add(active);
            }
            else if (active.Status == TopicStatus.Partial)
            {
                active.Status = TopicStatus.InProgress;
                active.Attempts = 0;
                active.BadgeEarned = null;
                active.CompletedAtUtc = null;
                active.LastActivityAtUtc = nowUtc;
            }
        }

        var seedForLesson = SeedCurriculumCatalog.Find(active.TopicId);
        return Task.FromResult(new LearningSnapshot(
            new ExplorerSummaryDto("Mía", Grade, null, "avatar-aguila.svg"),
            _progress.CurrentLevel,
            _progress.TotalXP,
            MapHistory(),
            SeedCurriculumCatalog.ToActiveLesson(seedForLesson),
            AvailableMissions: null,
            seedForLesson.SearchQuery));
    }

    public Task<IReadOnlyList<MissionOptionDto>> GetMissionOptionsAsync(Guid familyId, Guid childProfileId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MissionOptionDto>>(BuildMissionOptions(nowUtc));

    private List<MissionOptionDto> BuildMissionOptions(DateTime nowUtc)
    {
        var recommended = SeedCurriculumCatalog.SelectNextTopic(Grade, nowUtc, _topics);
        var options = SeedCurriculumCatalog.SelectMissionOptions(Grade, _topics, limit: 3);

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
                Status: _topics.FirstOrDefault(t => t.TopicId == topic.TopicId)?.Status switch
                {
                    TopicStatus.Completed => nameof(TopicStatus.Completed),
                    TopicStatus.Partial => nameof(TopicStatus.Partial),
                    _ => null
                }))
            .ToList();
    }

    public Task RecordActivityAsync(Guid childProfileId, Guid familyId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private List<TopicHistoryItemDto> MapHistory() =>
        _topics
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
}
