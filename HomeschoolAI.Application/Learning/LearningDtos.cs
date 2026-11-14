namespace HomeschoolAI.Application.Learning;

/// <summary>Bloque de diapositiva estilo NotebookLM (LessonJSON v1.0).</summary>
public sealed record LessonBlockDto(string Type, string Content, string AltText, string NarrationText);

/// <summary>Lección activa del tema en turno.</summary>
public sealed record ActiveLessonDto(
    string TopicId,
    string SchemaVersion,
    string Title,
    string CampoFormativo,
    string WeeklyProjectTitle,
    string WeeklyProjectGoal,
    IReadOnlyList<LessonBlockDto> Blocks);

/// <summary>Historial de un tema concreto del alumno.</summary>
public sealed record TopicHistoryItemDto(
    string TopicId,
    string TopicTitle,
    string CampoFormativo,
    string Status,
    int Attempts,
    int TimeSpentSeconds,
    string? BadgeEarned,
    DateTime LastActivityAtUtc);

/// <summary>Métricas agregadas de aprendizaje del alumno.</summary>
public sealed record LearningMetricsDto(
    int CurrentLevel,
    int TotalXp,
    int TotalTimeSpentSeconds,
    int AverageTimePerTopicSeconds,
    int CompletedTopicsCount,
    IReadOnlyList<string> BadgesEarned,
    IReadOnlyList<TopicHistoryItemDto> TopicHistory);

/// <summary>Resumen del explorador (perfil del niño).</summary>
public sealed record ExplorerSummaryDto(string ChildName, int Grade, int? Fase, string AvatarUrl);

/// <summary>Respuesta del inicio de sesión de aprendizaje proactiva.</summary>
public sealed record LearningSessionResponse(
    ExplorerSummaryDto Explorer,
    LearningMetricsDto Metrics,
    ActiveLessonDto ActiveLesson,
    string ThreadId,
    string InitialAxolotlMessage);

/// <summary>Snapshot que devuelve el repositorio de aprendizaje al caso de uso.</summary>
public sealed record LearningSnapshot(
    ExplorerSummaryDto Explorer,
    int CurrentLevel,
    int TotalXp,
    IReadOnlyList<TopicHistoryItemDto> TopicHistory,
    ActiveLessonDto ActiveLesson,
    string SearchQuery);