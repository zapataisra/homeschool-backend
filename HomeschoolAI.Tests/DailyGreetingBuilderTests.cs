using HomeschoolAI.Application.Chat;
using Xunit;

namespace HomeschoolAI.Tests;

public class DailyGreetingBuilderTests
{
    [Fact]
    public void First_time_absolute_greets_new_explorer()
    {
        var greeting = DailyGreetingBuilder.Build(
            "Mía", currentLevel: 1, completedTopicsCount: 0, totalXp: 0,
            hasHistory: false, hasActiveLesson: false, isNewDayReturn: false,
            weeklyProjectTitle: "", topicTitle: "");

        Assert.Equal(
            "¡Bienvenido/a al gremio de exploradores, Mía! 🎮 Soy Axolotl, tu compañero de aventuras. Elige tu primera misión aquí abajo para empezar a ganar XP.",
            greeting);
    }

    [Fact]
    public void New_project_with_history_greets_returning_explorer()
    {
        var greeting = DailyGreetingBuilder.Build(
            "Mía", currentLevel: 2, completedTopicsCount: 1, totalXp: 125,
            hasHistory: true, hasActiveLesson: false, isNewDayReturn: false,
            weeklyProjectTitle: "", topicTitle: "");

        Assert.Equal(
            "¡Qué alegría verte de nuevo, Mía (Nivel 2)! 🌟 Ya tienes 1 misión completada y 125 XP. ¿Qué nuevo proyecto quieres conquistar hoy?",
            greeting);
    }

    [Fact]
    public void Same_project_new_day_returns_to_mission()
    {
        var greeting = DailyGreetingBuilder.Build(
            "Mía", currentLevel: 2, completedTopicsCount: 0, totalXp: 25,
            hasHistory: true, hasActiveLesson: true, isNewDayReturn: true,
            weeklyProjectTitle: "La pizza fraccionaria", topicTitle: "Las fracciones");

        Assert.Equal(
            "¡Nuevo día de aventura, Mía! ☀️ Hoy seguimos con el proyecto de la clase de hoy: «La pizza fraccionaria» (Las fracciones). ¡Retomemos donde nos quedamos!",
            greeting);
    }

    [Fact]
    public void Same_project_same_day_keeps_adventure_going()
    {
        var greeting = DailyGreetingBuilder.Build(
            "Mía", currentLevel: 2, completedTopicsCount: 0, totalXp: 25,
            hasHistory: true, hasActiveLesson: true, isNewDayReturn: false,
            weeklyProjectTitle: "La pizza fraccionaria", topicTitle: "Las fracciones");

        Assert.Equal(
            "¡Aventura en marcha, Mía! 🚀 Hoy exploramos el proyecto de la clase de hoy: «La pizza fraccionaria» (Las fracciones).",
            greeting);
    }

    [Fact]
    public void Pluralizes_completed_missions()
    {
        var greeting = DailyGreetingBuilder.Build(
            "Mía", currentLevel: 3, completedTopicsCount: 3, totalXp: 350,
            hasHistory: true, hasActiveLesson: false, isNewDayReturn: false,
            weeklyProjectTitle: "", topicTitle: "");

        Assert.Contains("3 misiones completadas", greeting);
    }
}
