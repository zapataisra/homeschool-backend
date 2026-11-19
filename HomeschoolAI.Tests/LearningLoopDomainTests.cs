using HomeschoolAI.Adapters.Learning;
using HomeschoolAI.Domain.Progress;
using Xunit;

namespace HomeschoolAI.Tests;

public class LearningLoopDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RegisterAttempt_correct_on_first_attempt_completes_with_100_xp_and_rayo_badge()
    {
        var topic = NewTopic();

        var outcome = topic.RegisterAttempt(isCorrect: true, Now);

        Assert.Equal(TopicStatus.Completed, outcome.NewStatus);
        Assert.Equal(1, outcome.AttemptNumber);
        Assert.Equal(100, outcome.XpAwarded);
        Assert.Equal("Rayo de Inspiración", outcome.BadgeEarned);
        Assert.True(outcome.IsTopicClosed);
        Assert.Equal(TopicStatus.Completed, topic.Status);
        Assert.Equal(Now, topic.CompletedAtUtc);
        Assert.Equal("Rayo de Inspiración", topic.BadgeEarned);
    }

    [Fact]
    public void RegisterAttempt_correct_on_second_attempt_completes_with_75_xp()
    {
        var topic = NewTopic();
        topic.RegisterAttempt(isCorrect: false, Now);

        var outcome = topic.RegisterAttempt(isCorrect: true, Now);

        Assert.Equal(TopicStatus.Completed, outcome.NewStatus);
        Assert.Equal(2, outcome.AttemptNumber);
        Assert.Equal(75, outcome.XpAwarded);
        Assert.Equal("Exploradora Curiosa", outcome.BadgeEarned);
        Assert.True(outcome.IsTopicClosed);
    }

    [Fact]
    public void RegisterAttempt_correct_on_third_attempt_completes_with_50_xp()
    {
        var topic = NewTopic();
        topic.RegisterAttempt(isCorrect: false, Now);
        topic.RegisterAttempt(isCorrect: false, Now);

        var outcome = topic.RegisterAttempt(isCorrect: true, Now);

        Assert.Equal(TopicStatus.Completed, outcome.NewStatus);
        Assert.Equal(3, outcome.AttemptNumber);
        Assert.Equal(50, outcome.XpAwarded);
        Assert.Equal("Imparable + Cristal de Perseverancia", outcome.BadgeEarned);
        Assert.True(outcome.IsTopicClosed);
    }

    [Fact]
    public void RegisterAttempt_wrong_on_first_attempt_stays_in_progress_without_xp()
    {
        var topic = NewTopic();

        var outcome = topic.RegisterAttempt(isCorrect: false, Now);

        Assert.Equal(TopicStatus.InProgress, outcome.NewStatus);
        Assert.Equal(1, outcome.AttemptNumber);
        Assert.Equal(0, outcome.XpAwarded);
        Assert.Null(outcome.BadgeEarned);
        Assert.False(outcome.IsTopicClosed);
        Assert.Equal(TopicStatus.InProgress, topic.Status);
    }

    [Fact]
    public void RegisterAttempt_wrong_on_third_attempt_pauses_topic_with_25_xp()
    {
        var topic = NewTopic();
        topic.RegisterAttempt(isCorrect: false, Now);
        topic.RegisterAttempt(isCorrect: false, Now);

        var outcome = topic.RegisterAttempt(isCorrect: false, Now);

        Assert.Equal(TopicStatus.Partial, outcome.NewStatus);
        Assert.Equal(3, outcome.AttemptNumber);
        Assert.Equal(25, outcome.XpAwarded);
        Assert.Null(outcome.BadgeEarned);
        Assert.True(outcome.IsTopicClosed);
        Assert.Equal(TopicStatus.Partial, topic.Status);
        Assert.Null(topic.CompletedAtUtc);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(199, 2)]
    [InlineData(200, 3)]
    [InlineData(300, 4)]
    public void ChildProgress_level_uses_100_xp_per_level(int totalXp, int expectedLevel)
    {
        Assert.Equal(ChildProgress.XpPerLevel, 100);
        Assert.Equal(expectedLevel, ChildProgress.CalculateLevel(totalXp));
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 1)]
    [InlineData(11, 1)]
    [InlineData(12, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    [InlineData(7, 3)]
    public void GetSepTrimesterForMonth_maps_months_to_trimesters(int month, int expectedTrimester)
    {
        Assert.Equal(expectedTrimester, SeedCurriculumCatalog.GetSepTrimesterForMonth(month));
    }

    [Fact]
    public void SelectNextTopic_continues_the_in_progress_topic()
    {
        var topics = new List<ChildTopicProgress> { Topic("fase4-saberes-fracciones", TopicStatus.InProgress, Now) };

        var next = SeedCurriculumCatalog.SelectNextTopic(4, Now, topics);

        Assert.Equal("fase4-saberes-fracciones", next.TopicId);
    }

    [Fact]
    public void SelectNextTopic_returns_prerequisite_when_last_topic_is_partial()
    {
        // Grado 4: "Las fracciones" (OrderIndex 2) quedó Partial; su prerrequisito
        // "Repartos y fracciones equivalentes" no está completado.
        var topics = new List<ChildTopicProgress> { Topic("fase4-saberes-fracciones", TopicStatus.Partial, Now) };

        var next = SeedCurriculumCatalog.SelectNextTopic(4, Now, topics);

        Assert.Equal("fase4-saberes-repartos", next.TopicId);
    }

    [Fact]
    public void SelectNextTopic_new_student_selects_first_topic_of_current_trimester()
    {
        var november = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
        var january = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var nextInNovember = SeedCurriculumCatalog.SelectNextTopic(4, november, Array.Empty<ChildTopicProgress>());
        var nextInJanuary = SeedCurriculumCatalog.SelectNextTopic(4, january, Array.Empty<ChildTopicProgress>());

        Assert.Equal("fase4-saberes-repartos", nextInNovember.TopicId);   // Trimestre 1
        Assert.Equal("fase4-saberes-fracciones", nextInJanuary.TopicId);  // Trimestre 2
    }

    [Fact]
    public void SelectNextTopic_advances_to_next_topic_not_completed()
    {
        var topics = new List<ChildTopicProgress> { Topic("fase4-saberes-repartos", TopicStatus.Completed, Now) };

        var next = SeedCurriculumCatalog.SelectNextTopic(4, Now, topics);

        Assert.Equal("fase4-saberes-fracciones", next.TopicId);
    }

    private static ChildTopicProgress NewTopic() => Topic("fase4-saberes-fracciones", TopicStatus.InProgress, Now);

    private static ChildTopicProgress Topic(string topicId, TopicStatus status, DateTime lastActivity) =>
        new()
        {
            ChildProfileId = Guid.NewGuid(),
            TopicId = topicId,
            TopicTitle = topicId,
            CampoFormativo = "Saberes y Pensamiento Científico",
            Status = status,
            StartedAtUtc = lastActivity,
            LastActivityAtUtc = lastActivity
        };
}
