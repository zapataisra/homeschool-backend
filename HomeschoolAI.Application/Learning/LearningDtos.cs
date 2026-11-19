using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Application.Learning;

/// <summary>Bloque de diapositiva estilo NotebookLM (LessonJSON v1.0).</summary>
public sealed record LessonBlockDto(string Type, string Content, string AltText, string NarrationText, string? ImageUrl = null);

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

/// <summary>Opción de misión semanal ofrecida al alumno (selector de 3 misiones).</summary>
public sealed record MissionOptionDto(
    string TopicId,
    string Title,
    string CampoFormativo,
    string WeeklyProjectTitle,
    string WeeklyProjectGoal,
    int Trimester,
    int OrderIndex,
    bool IsRecommended,
    string? Status);

/// <summary>Respuesta del inicio de sesión de aprendizaje proactiva.</summary>
public sealed record LearningSessionResponse(
    ExplorerSummaryDto Explorer,
    LearningMetricsDto Metrics,
    ActiveLessonDto? ActiveLesson,
    string ThreadId,
    string InitialAxolotlMessage,
    IReadOnlyList<MissionOptionDto>? AvailableMissions = null,
    string DailyGreeting = "",
    IReadOnlyList<ConversationMessageDto>? Messages = null);

/// <summary>Snapshot que devuelve el repositorio de aprendizaje al caso de uso.</summary>
public sealed record LearningSnapshot(
    ExplorerSummaryDto Explorer,
    int CurrentLevel,
    int TotalXp,
    IReadOnlyList<TopicHistoryItemDto> TopicHistory,
    ActiveLessonDto? ActiveLesson,
    IReadOnlyList<MissionOptionDto>? AvailableMissions,
    string SearchQuery,
    bool IsNewDayReturn = false);

/// <summary>Resultado de aplicar un intento: desenlace de la máquina de estados + métricas actualizadas.</summary>
public sealed record AttemptOutcomeDto(
    AttemptEvaluationOutcome Outcome,
    LearningMetricsDto Metrics);

/// <summary>Tema en curso (InProgress) del niño, con la consulta curricular para recuperar sus fragmentos.</summary>
public sealed record ActiveTopicDto(
    string TopicId,
    string TopicTitle,
    string CampoFormativo,
    string SearchQuery,
    int Attempts,
    int Grade,
    string WeeklyProjectGoal);

/// <summary>Mensaje de conversación persistido (turno de usuario o de asistente).</summary>
public sealed record ConversationMessageDto(string Role, string Content);