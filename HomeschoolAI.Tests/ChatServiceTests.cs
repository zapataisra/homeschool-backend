using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HomeschoolAI.Application.Chat;
using HomeschoolAI.Domain.Auth;
using HomeschoolAI.Domain.Conversation;
using Xunit;

namespace HomeschoolAI.Tests;

public class ChatServiceTests
{
    private const string UserA = "member-a";
    private const string UserB = "member-b";

    private static ChatService BuildService(
        FakeConversationStore store,
        FakeAgentCompletionService completion,
        string userId,
        ILogger<ChatService> logger) =>
        new(store, completion, new FakeUserContextProvider(new UserContext(userId, Guid.NewGuid(), UserRole.Owner)), logger);

    [Fact]
    public async Task Chat_generates_thread_id_and_persists_user_and_assistant_turns()
    {
        var store = new FakeConversationStore();
        var completion = new FakeAgentCompletionService { Reply = "Los campos formativos son..." };
        var service = BuildService(store, completion, UserA, NullLogger<ChatService>.Instance);

        var response = await service.SendAsync(new ChatRequest { Message = "¿Qué es un campo formativo?" });

        Assert.False(string.IsNullOrWhiteSpace(response.ThreadId));
        Assert.Equal("Los campos formativos son...", response.Content);

        var history = store.Get(UserA, response.ThreadId);
        Assert.NotNull(history);
        Assert.Equal(2, history!.Messages.Count);
        Assert.Equal(ConversationRole.User, history.Messages[0].Role);
        Assert.Equal("¿Qué es un campo formativo?", history.Messages[0].Content);
        Assert.Equal(ConversationRole.Assistant, history.Messages[1].Role);
    }

    [Fact]
    public async Task Chat_continues_existing_thread_accumulating_history()
    {
        var store = new FakeConversationStore();
        var completion = new FakeAgentCompletionService { Reply = "respuesta" };
        var service = BuildService(store, completion, UserA, NullLogger<ChatService>.Instance);

        var first = await service.SendAsync(new ChatRequest { Message = "primera" });
        var second = await service.SendAsync(new ChatRequest { ThreadId = first.ThreadId, Message = "segunda" });

        Assert.Equal(first.ThreadId, second.ThreadId);

        var history = store.Get(UserA, first.ThreadId);
        Assert.NotNull(history);
        Assert.Equal(4, history!.Messages.Count);
        Assert.Equal("primera", history.Messages[0].Content);
        Assert.Equal("segunda", history.Messages[2].Content);
    }

    [Fact]
    public async Task Chat_trims_history_to_the_cap()
    {
        var store = new FakeConversationStore();
        var completion = new FakeAgentCompletionService { Reply = "respuesta" };
        var service = BuildService(store, completion, UserA, NullLogger<ChatService>.Instance);

        var first = await service.SendAsync(new ChatRequest { Message = "turno 0" });
        for (var i = 1; i <= 11; i++)
        {
            await service.SendAsync(new ChatRequest { ThreadId = first.ThreadId, Message = $"turno {i}" });
        }

        var history = store.Get(UserA, first.ThreadId);
        Assert.NotNull(history);
        Assert.Equal(20, history!.Messages.Count);
    }

    [Fact]
    public async Task Chat_isolates_threads_between_users()
    {
        var store = new FakeConversationStore();
        var completion = new FakeAgentCompletionService { Reply = "respuesta" };

        var serviceA = BuildService(store, completion, UserA, NullLogger<ChatService>.Instance);
        var serviceB = BuildService(store, completion, UserB, NullLogger<ChatService>.Instance);

        var firstA = await serviceA.SendAsync(new ChatRequest { Message = "secreto de A" });

        // B intenta continuar el hilo de A usando el mismo threadId.
        await serviceB.SendAsync(new ChatRequest { ThreadId = firstA.ThreadId, Message = "continuo" });

        var historyB = store.Get(UserB, firstA.ThreadId);
        Assert.NotNull(historyB);
        Assert.Equal(2, historyB!.Messages.Count);
        Assert.DoesNotContain(historyB.Messages, m => m.Content == "secreto de A");

        var historyA = store.Get(UserA, firstA.ThreadId);
        Assert.NotNull(historyA);
        Assert.Equal(2, historyA!.Messages.Count);
        Assert.Contains(historyA.Messages, m => m.Content == "secreto de A");
    }

    [Fact]
    public async Task Chat_does_not_log_question_text()
    {
        const string secretQuestion = "PREGUNTA_SECRETA_AGENTE_123";

        var sink = new InMemoryLogSink();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(sink));

        var store = new FakeConversationStore();
        var completion = new FakeAgentCompletionService();
        var service = BuildService(store, completion, UserA, loggerFactory.CreateLogger<ChatService>());

        await service.SendAsync(new ChatRequest { Message = secretQuestion });

        var allLogs = string.Join('\n', sink.Messages);

        Assert.NotEmpty(sink.Messages);
        Assert.Contains("Chat completado", allLogs);
        Assert.Contains(secretQuestion.Length.ToString(), allLogs);
        Assert.DoesNotContain(secretQuestion, allLogs);
    }
}
