using Microsoft.Extensions.Logging;
using Pgvector;
using HomeschoolAI.Domain.Conversation;
using HomeschoolAI.Domain.Embedding;
using HomeschoolAI.Domain.Repositories;
using HomeschoolAI.Domain.Retrieval;

namespace HomeschoolAI.Tests;

public sealed class FakeEmbeddingRepository : IEmbeddingRepository
{
    private readonly SearchOutcome _outcome;

    public FakeEmbeddingRepository(SearchOutcome outcome) => _outcome = outcome;

    public Task<SearchOutcome> SearchAsync(
        Vector? queryVector,
        string question,
        SearchMode mode,
        int limit,
        int pool,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_outcome);
}

public sealed class FakeQueryEmbedder : IQueryEmbedder
{
    private readonly float[] _vector;

    public FakeQueryEmbedder(float[] vector) => _vector = vector;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(_vector);
}

public sealed class FakeConversationStore : IConversationStore
{
    private readonly Dictionary<string, ConversationHistory> _threads = new();

    public Task<ConversationHistory> GetOrCreateAsync(string threadId, CancellationToken cancellationToken = default)
    {
        if (_threads.TryGetValue(threadId, out var history))
        {
            return Task.FromResult(history);
        }

        return Task.FromResult(new ConversationHistory(threadId, Array.Empty<ConversationMessage>()));
    }

    public Task SaveAsync(ConversationHistory history, CancellationToken cancellationToken = default)
    {
        _threads[history.ThreadId] = history;
        return Task.CompletedTask;
    }

    public ConversationHistory? Get(string threadId) =>
        _threads.TryGetValue(threadId, out var history) ? history : null;
}

public sealed class FakeAgentCompletionService : IAgentCompletionService
{
    public string Reply { get; set; } = "respuesta simulada";

    public int Calls { get; private set; }

    public Task<string> CompleteAsync(ConversationHistory history, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(Reply);
    }
}

public sealed class InMemoryLogSink : ILoggerProvider
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName) => new SinkLogger(_messages);

    public void Dispose()
    {
    }

    private sealed class SinkLogger : ILogger
    {
        private readonly List<string> _messages;

        public SinkLogger(List<string> messages) => _messages = messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _messages.Add(formatter(state, exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
