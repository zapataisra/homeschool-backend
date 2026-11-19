namespace HomeschoolAI.Application.Chat;

/// <summary>
/// Saludo diario estilo videojuego, 100% determinista (0 llamadas al LLM).
/// </summary>
public static class DailyGreetingBuilder
{
    public static string Build(
        string explorerName,
        int currentLevel,
        int completedTopicsCount,
        int totalXp,
        bool hasHistory,
        bool hasActiveLesson,
        bool isNewDayReturn,
        string weeklyProjectTitle,
        string topicTitle)
    {
        if (!hasActiveLesson)
        {
            if (!hasHistory)
            {
                return $"¡Bienvenido/a al gremio de exploradores, {explorerName}! 🎮 Soy Axolotl, tu compañero de aventuras. Elige tu primera misión aquí abajo para empezar a ganar XP.";
            }

            return $"¡Qué alegría verte de nuevo, {explorerName} (Nivel {currentLevel})! 🌟 Ya tienes {MissionsLabel(completedTopicsCount)} y {totalXp} XP. ¿Qué nuevo proyecto quieres conquistar hoy?";
        }

        if (isNewDayReturn)
        {
            return $"¡Nuevo día de aventura, {explorerName}! ☀️ Hoy seguimos con el proyecto de la clase de hoy: «{weeklyProjectTitle}» ({topicTitle}). ¡Retomemos donde nos quedamos!";
        }

        return $"¡Aventura en marcha, {explorerName}! 🚀 Hoy exploramos el proyecto de la clase de hoy: «{weeklyProjectTitle}» ({topicTitle}).";
    }

    private static string MissionsLabel(int count) =>
        count == 1 ? "1 misión completada" : $"{count} misiones completadas";
}