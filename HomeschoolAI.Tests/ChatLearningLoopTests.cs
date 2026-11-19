using Microsoft.Extensions.Logging.Abstractions;
using HomeschoolAI.Application.Chat;
using HomeschoolAI.Application.Dtos;
using HomeschoolAI.Application.Evaluation;
using HomeschoolAI.Application.Progress;
using HomeschoolAI.Domain.Auth;
using HomeschoolAI.Domain.Conversation;
using HomeschoolAI.Domain.Progress;
using HomeschoolAI.Domain.Validation;
using Xunit;

namespace HomeschoolAI.Tests;

public class ChatLearningLoopTests
{
    private const string CriticalMarker = "extractedOperation";

    private static readonly SearchChunkDto Fragment = new(1, "Programa_Sintetico_Fase_4.pdf", 42, "Las fracciones equivalentes representan la misma cantidad.", 0.9);

    [Fact]
    public async Task Correct_answer_on_first_attempt_completes_topic_and_prepares_next_lesson()
    {
        var repo = new InMemoryChildLearningRepository { Grade = 4 };
        repo.Seed("fase4-saberes-repartos", TopicStatus.Completed);
        repo.Seed("fase4-saberes-fracciones", TopicStatus.InProgress);

        var completion = new FakeAgentCompletionService
        {
            ReplySelector = prompt => prompt.Contains(CriticalMarker)
                ? EvaluationJson(precision: 100, expression: "4 * 3 = 12")
                : "¡Lo lograste, Mía! Ganaste la insignia Rayo de Inspiración y 100 XP. 🌟"
        };

        var service = BuildService(completion, repo);
        var response = await service.SendAsync(new ChatRequest { Message = "4 por 3 es 12" });

        Assert.NotNull(response.AttemptOutcome);
        Assert.Equal(TopicStatus.Completed, response.AttemptOutcome!.Outcome.NewStatus);
        Assert.Equal(1, response.AttemptOutcome.Outcome.AttemptNumber);
        Assert.Equal(100, response.AttemptOutcome.Outcome.XpAwarded);
        Assert.Equal("Rayo de Inspiración", response.AttemptOutcome.Outcome.BadgeEarned);
        Assert.True(response.AttemptOutcome.Outcome.IsTopicClosed);

        Assert.NotNull(response.Metrics);
        Assert.Equal(100, response.Metrics!.TotalXp);
        Assert.Equal(2, response.Metrics.CurrentLevel);

        Assert.NotNull(response.NextLesson);
        Assert.Equal("fase4-etica-derechos", response.NextLesson!.TopicId);

        // El cierre de Axolotl debe prohibir nuevas preguntas y celebrar con XP/insignia.
        Assert.Contains(completion.SystemPrompts, p => p.Contains("MISIÓN COMPLETADA"));
        Assert.Contains(completion.SystemPrompts, p => p.Contains("PROHIBIDO hacer más preguntas"));
        Assert.Contains(completion.SystemPrompts, p => p.Contains("+100 XP"));
        Assert.Contains(completion.SystemPrompts, p => p.Contains("Rayo de Inspiración"));
    }

    [Fact]
    public async Task False_math_forces_failure_even_if_llm_says_correct()
    {
        var repo = new InMemoryChildLearningRepository { Grade = 4 };
        repo.Seed("fase4-saberes-fracciones", TopicStatus.InProgress);

        var completion = new FakeAgentCompletionService
        {
            ReplySelector = prompt => prompt.Contains(CriticalMarker)
                ? EvaluationJson(precision: 100, expression: "4 * 3 = 15", reformulationHint: "Recuerda que 4 grupos de 3 son 12.")
                : "¡Estuviste cerca! Pensemos de otra forma. 🌿"
        };

        var service = BuildService(completion, repo);
        var response = await service.SendAsync(new ChatRequest { Message = "4 por 3 es 15" });

        Assert.NotNull(response.AttemptOutcome);
        Assert.Equal(TopicStatus.InProgress, response.AttemptOutcome!.Outcome.NewStatus);
        Assert.Equal(1, response.AttemptOutcome.Outcome.AttemptNumber);
        Assert.Equal(0, response.AttemptOutcome.Outcome.XpAwarded);
        Assert.False(response.AttemptOutcome.Outcome.IsTopicClosed);
        Assert.Null(response.NextLesson);
        Assert.Equal(0, repo.Progress.TotalXP);

        Assert.Contains(completion.SystemPrompts, p => p.Contains("Intento 1 de 3"));
        Assert.Contains(completion.SystemPrompts, p => p.Contains("Usa la pista «Recuerda que 4 grupos de 3 son 12.»"));
        Assert.Contains(completion.SystemPrompts, p => p.Contains("SIN cambiar de tema"));
        Assert.Contains(completion.SystemPrompts, p => p.Contains("Termina volviendo a preguntar"));
    }

    [Fact]
    public async Task Third_failed_attempt_pauses_topic_and_points_to_prerequisite()
    {
        var repo = new InMemoryChildLearningRepository { Grade = 4 };
        repo.Seed("fase4-saberes-fracciones", TopicStatus.InProgress, attempts: 2);

        var completion = new FakeAgentCompletionService
        {
            ReplySelector = prompt => prompt.Contains(CriticalMarker)
                ? EvaluationJson(precision: 20, expression: null)
                : "Hiciste un gran esfuerzo. Hoy pausaremos este reto. 🌽"
        };

        var service = BuildService(completion, repo);
        var response = await service.SendAsync(new ChatRequest { Message = "no entiendo" });

        Assert.NotNull(response.AttemptOutcome);
        Assert.Equal(TopicStatus.Partial, response.AttemptOutcome!.Outcome.NewStatus);
        Assert.Equal(3, response.AttemptOutcome.Outcome.AttemptNumber);
        Assert.Equal(25, response.AttemptOutcome.Outcome.XpAwarded);
        Assert.True(response.AttemptOutcome.Outcome.IsTopicClosed);

        Assert.NotNull(response.NextLesson);
        Assert.Equal("fase4-saberes-repartos", response.NextLesson!.TopicId);

        // El cierre por agotamiento de intentos no debe terminar con otra pregunta.
        Assert.Contains(completion.SystemPrompts, p => p.Contains("PROHIBIDO terminar este mensaje con una pregunta"));
    }

    [Fact]
    public async Task SendAsync_passes_last_assistant_question_in_challenge_context()
    {
        var repo = new InMemoryChildLearningRepository { Grade = 4 };
        repo.Seed("fase4-etica-derechos", TopicStatus.InProgress);

        const string threadId = "lesson-fase4-etica-derechos";
        const string question = "¿Cuál crees que es un derecho muy importante que todos los niños y niñas deberían tener, y por qué?";

        // El tutor Axolotl ya había hecho su pregunta en el turno anterior.
        var store = new FakeConversationStore();
        await store.SaveAsync(
            ChildId.ToString("N"),
            new ConversationHistory(threadId, new[] { new ConversationMessage(ConversationRole.Assistant, question) }));

        var completion = new FakeAgentCompletionService
        {
            ReplySelector = prompt => prompt.Contains(CriticalMarker)
                ? EvaluationJson(precision: 100, expression: null)
                : "¡Excelente, Mía! Ganaste 100 XP y la insignia Rayo de Inspiración. 🌟"
        };

        var service = BuildService(completion, repo, store);
        await service.SendAsync(new ChatRequest { ThreadId = threadId, Message = "derecho a la educación, porque debemos aprender cosas en la escuela" });

        var criticalIndex = completion.SystemPrompts.FindIndex(p => p.Contains(CriticalMarker));
        Assert.True(criticalIndex >= 0, "Se esperaba una llamada al Agente Crítico.");
        Assert.Contains(question, completion.HistoryContents[criticalIndex]);
    }

    [Fact]
    public async Task Completed_topic_sanitizes_axolotl_reply()
    {
        var repo = new InMemoryChildLearningRepository { Grade = 4 };
        repo.Seed("fase4-saberes-repartos", TopicStatus.Completed);
        repo.Seed("fase4-saberes-fracciones", TopicStatus.InProgress);

        // El LLM "se porta mal": incluye asteriscos de Markdown, una pregunta y una frase de "casi lo logras".
        var completion = new FakeAgentCompletionService
        {
            ReplySelector = prompt => prompt.Contains(CriticalMarker)
                ? EvaluationJson(precision: 100, expression: "4 * 3 = 12")
                : "**¡Estuviste muy cerca!** ¿Verdad que fue divertido? Ganaste +100 XP y la insignia Rayo de Inspiración. 🌟"
        };

        var service = BuildService(completion, repo);
        var response = await service.SendAsync(new ChatRequest { Message = "4 por 3 es 12" });

        Assert.NotNull(response.AttemptOutcome);
        Assert.Equal(TopicStatus.Completed, response.AttemptOutcome!.Outcome.NewStatus);
        Assert.True(response.AttemptOutcome.Outcome.IsTopicClosed);

        Assert.DoesNotContain("**", response.Content);
        Assert.DoesNotContain("?", response.Content);
        Assert.DoesNotContain("¿", response.Content);
        Assert.False(response.Content.Contains("estuviste muy cerca", StringComparison.OrdinalIgnoreCase));
        Assert.False(response.Content.Contains("casi lo lograste", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Non_equivalent_fractions_in_natural_language_do_not_complete_topic()
    {
        var repo = new InMemoryChildLearningRepository { Grade = 4 };
        repo.Seed("fase4-saberes-repartos", TopicStatus.Completed);
        repo.Seed("fase4-saberes-fracciones", TopicStatus.InProgress);

        // El LLM (Agente Crítico) "aprueba" por error (precision 100, sin extraer matemáticas),
        // pero la capa determinista debe detectar que 8/24 NO es equivalente a 8/12.
        var completion = new FakeAgentCompletionService
        {
            ReplySelector = prompt => prompt.Contains(CriticalMarker)
                ? EvaluationJson(precision: 100, expression: null)
                : "¡Uy! 8/24 y 8/12 no son equivalentes. ¿Quieres intentarlo de nuevo? 🌿"
        };

        var service = BuildService(completion, repo);
        var response = await service.SendAsync(new ChatRequest { Message = "dividirla en 24 y comer 8, es igual a dividirla 12 y comer 8" });

        Assert.NotNull(response.AttemptOutcome);
        Assert.Equal(TopicStatus.InProgress, response.AttemptOutcome!.Outcome.NewStatus);
        Assert.Equal(0, response.AttemptOutcome.Outcome.XpAwarded);
        Assert.False(response.AttemptOutcome.Outcome.IsTopicClosed);
        Assert.Contains("?", response.Content);
    }

    [Fact]
    public async Task Equivalent_fractions_in_natural_language_complete_topic()
    {
        var repo = new InMemoryChildLearningRepository { Grade = 4 };
        repo.Seed("fase4-saberes-repartos", TopicStatus.Completed);
        repo.Seed("fase4-saberes-fracciones", TopicStatus.InProgress);

        var completion = new FakeAgentCompletionService
        {
            ReplySelector = prompt => prompt.Contains(CriticalMarker)
                ? EvaluationJson(precision: 100, expression: null)
                : "¡Excelente! 4/8 y 2/4 son fracciones equivalentes. Ganaste +100 XP. 🌟"
        };

        var service = BuildService(completion, repo);
        var response = await service.SendAsync(new ChatRequest { Message = "una pizza, la puedo dividir en 8 y tomar 4, que serian 4/8 o dividirla en 4 y tomar 2 osea 2/4" });

        Assert.NotNull(response.AttemptOutcome);
        Assert.Equal(TopicStatus.Completed, response.AttemptOutcome!.Outcome.NewStatus);
        Assert.True(response.AttemptOutcome.Outcome.XpAwarded > 0);
        Assert.True(response.AttemptOutcome.Outcome.IsTopicClosed);
    }

    private static readonly Guid ChildId = Guid.NewGuid();

    private static ChatService BuildService(FakeAgentCompletionService completion, InMemoryChildLearningRepository repo, FakeConversationStore? store = null)
    {
        var evaluator = new CriticalEvaluationService(
            new DeterministicMathValidator(),
            new XPCalculator(),
            completion,
            NullLogger<CriticalEvaluationService>.Instance);

        var user = new UserContext("member-1", Guid.NewGuid(), UserRole.Child, ChildId, "Mía");

        return new ChatService(
            store ?? new FakeConversationStore(),
            completion,
            new FakeUserContextProvider(user),
            new FakeInteractionQuotaService(),
            repo,
            new FakeSearchService(new[] { Fragment }),
            evaluator,
            NullLogger<ChatService>.Instance);
    }

    private static string EvaluationJson(int precision, string? expression, string? reformulationHint = null)
    {
        var operation = expression is null
            ? "null"
            : $"{{\"expression\":\"{expression}\",\"operand1\":0,\"operation\":\"\",\"operand2\":0,\"proposedResult\":0}}";

        var hint = reformulationHint is null ? "null" : $"\"{reformulationHint}\"";

        return $"{{\"precision\":{precision},\"confidence\":0.9,\"missingConcepts\":[],\"reformulationHint\":{hint},\"extractedOperation\":{operation},\"sourceChunkIds\":[1]}}";
    }
}
