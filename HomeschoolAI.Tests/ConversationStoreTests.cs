using Microsoft.Extensions.Logging.Abstractions;
using HomeschoolAI.Adapters.Persistence;
using HomeschoolAI.Domain.Conversation;
using Xunit;

namespace HomeschoolAI.Tests;

public class ConversationStoreTests
{
    private const string UserA = "user-a";
    private const string UserB = "user-b";

    [Fact]
    public async Task Store_returns_empty_history_for_unknown_thread()
    {
        var store = new InMemoryConversationStore(NullLogger<InMemoryConversationStore>.Instance);

        var history = await store.GetOrCreateAsync(UserA, "abc");

        Assert.Equal("abc", history.ThreadId);
        Assert.Empty(history.Messages);
    }

    [Fact]
    public async Task Store_round_trips_history_per_thread()
    {
        var store = new InMemoryConversationStore(NullLogger<InMemoryConversationStore>.Instance);

        var messages = new ConversationMessage[]
        {
            new(ConversationRole.User, "hola"),
            new(ConversationRole.Assistant, "¡Hola! ¿En qué te ayudo?")
        };

        await store.SaveAsync(UserA, new ConversationHistory("hilo-1", messages));

        var history = await store.GetOrCreateAsync(UserA, "hilo-1");
        Assert.Equal(2, history.Messages.Count);
        Assert.Equal(ConversationRole.Assistant, history.Messages[1].Role);

        var other = await store.GetOrCreateAsync(UserA, "hilo-2");
        Assert.Empty(other.Messages);
    }

    [Fact]
    public async Task Store_isolates_threads_by_user()
    {
        var store = new InMemoryConversationStore(NullLogger<InMemoryConversationStore>.Instance);

        await store.SaveAsync(UserA, new ConversationHistory("t1", new[] { new ConversationMessage(ConversationRole.User, "hola") }));

        var historyA = await store.GetOrCreateAsync(UserA, "t1");
        Assert.Single(historyA.Messages);

        var historyB = await store.GetOrCreateAsync(UserB, "t1");
        Assert.Empty(historyB.Messages);
    }

    [Fact]
    public async Task Store_expires_thread_after_idle_timeout()
    {
        var store = new InMemoryConversationStore(
            NullLogger<InMemoryConversationStore>.Instance,
            idleTimeout: TimeSpan.FromMilliseconds(100));

        await store.SaveAsync(UserA, new ConversationHistory(
            "expirable",
            new[] { new ConversationMessage(ConversationRole.User, "hola") }));

        await Task.Delay(300);

        var history = await store.GetOrCreateAsync(UserA, "expirable");
        Assert.Empty(history.Messages);
    }
}
