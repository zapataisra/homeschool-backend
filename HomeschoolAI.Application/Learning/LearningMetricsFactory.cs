using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Application.Learning;

/// <summary>
/// Construye el <see cref="LearningMetricsDto"/> agregado a partir del nivel, la XP
/// y el historial de temas. Vive una sola vez para que el caso de uso de chat y el
/// repositorio de aprendizaje calculen las mismas métricas.
/// </summary>
public static class LearningMetricsFactory
{
    public static LearningMetricsDto Build(int currentLevel, int totalXp, IReadOnlyList<TopicHistoryItemDto> history)
    {
        var totalTime = history.Sum(t => t.TimeSpentSeconds);
        var average = history.Count > 0 ? totalTime / history.Count : 0;
        var completed = history.Count(t => t.Status == nameof(TopicStatus.Completed));
        var badges = history
            .Where(t => t.BadgeEarned is not null)
            .Select(t => t.BadgeEarned!)
            .Distinct()
            .ToList();

        return new LearningMetricsDto(currentLevel, totalXp, totalTime, average, completed, badges, history);
    }
}
