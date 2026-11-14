using HomeschoolAI.Domain.Progress;
using Xunit;

namespace HomeschoolAI.Tests;

public class ChildTopicProgressTests
{
    [Fact]
    public void RecordActivity_accumulates_elapsed_seconds()
    {
        var progress = NewProgress(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));

        progress.RecordActivity(new DateTime(2026, 1, 1, 10, 0, 30, DateTimeKind.Utc));

        Assert.Equal(30, progress.TimeSpentSeconds);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 0, 30, DateTimeKind.Utc), progress.LastActivityAtUtc);
    }

    [Fact]
    public void RecordActivity_accumulates_across_multiple_turns()
    {
        var progress = NewProgress(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));

        progress.RecordActivity(new DateTime(2026, 1, 1, 10, 0, 20, DateTimeKind.Utc));
        progress.RecordActivity(new DateTime(2026, 1, 1, 10, 0, 50, DateTimeKind.Utc));

        Assert.Equal(50, progress.TimeSpentSeconds);
    }

    [Fact]
    public void RecordActivity_caps_idle_time_at_600_seconds()
    {
        var progress = NewProgress(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));

        // 30 minutos de inactividad (1800 s): solo se acumulan los 600 s máximos.
        progress.RecordActivity(new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Utc));

        Assert.Equal(600, progress.TimeSpentSeconds);
    }

    [Fact]
    public void RecordActivity_does_not_accumulate_when_clock_goes_backwards()
    {
        var progress = NewProgress(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));

        progress.RecordActivity(new DateTime(2026, 1, 1, 9, 59, 59, DateTimeKind.Utc));

        Assert.Equal(0, progress.TimeSpentSeconds);
    }

    private static ChildTopicProgress NewProgress(DateTime startedAtUtc) =>
        new()
        {
            ChildProfileId = Guid.NewGuid(),
            TopicId = "fase4-saberes-ciclo-agua",
            TopicTitle = "El ciclo del agua",
            CampoFormativo = "Saberes y Pensamiento Científico",
            StartedAtUtc = startedAtUtc,
            LastActivityAtUtc = startedAtUtc
        };
}