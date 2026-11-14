using Microsoft.Extensions.Logging;
using Pgvector;
using HomeschoolAI.Application.Dtos;
using HomeschoolAI.Application.Learning;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Application.Search;
using HomeschoolAI.Domain.Auth;
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

    public Task<ConversationHistory> GetOrCreateAsync(string userId, string threadId, CancellationToken cancellationToken = default)
    {
        var key = $"{userId}:{threadId}";
        if (_threads.TryGetValue(key, out var history))
        {
            return Task.FromResult(history);
        }

        return Task.FromResult(new ConversationHistory(threadId, Array.Empty<ConversationMessage>()));
    }

    public Task SaveAsync(string userId, ConversationHistory history, CancellationToken cancellationToken = default)
    {
        _threads[$"{userId}:{history.ThreadId}"] = history;
        return Task.CompletedTask;
    }

    public ConversationHistory? Get(string userId, string threadId) =>
        _threads.TryGetValue($"{userId}:{threadId}", out var history) ? history : null;
}

public sealed class FakeUserContextProvider : IUserContextProvider
{
    private readonly UserContext? _context;

    public FakeUserContextProvider(UserContext? context = null) => _context = context;

    public Task<UserContext?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_context);
}

public sealed class FakeAgentCompletionService : IAgentCompletionService
{
    public string Reply { get; set; } = "respuesta simulada";

    public int Calls { get; private set; }

    public string? LastSystemPrompt { get; private set; }

    public Task<string> CompleteAsync(ConversationHistory history, string systemPrompt, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastSystemPrompt = systemPrompt;
        return Task.FromResult(Reply);
    }
}

public sealed class FakeInteractionQuotaService : IInteractionQuotaService
{
    public int ConsumedCalls { get; private set; }

    public Task ConsumeInteractionAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        ConsumedCalls++;
        return Task.CompletedTask;
    }
}

public sealed class FakeSearchService : ISearchService
{
    private readonly IReadOnlyList<SearchChunkDto> _results;

    public FakeSearchService(IReadOnlyList<SearchChunkDto>? results = null) => _results = results ?? Array.Empty<SearchChunkDto>();

    public Task<IReadOnlyList<SearchChunkDto>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(_results);
}

public sealed class FakeChildLearningRepository : IChildLearningRepository
{
    public Task<LearningSnapshot> GetOrCreateActiveLessonAsync(Guid childProfileId, Guid familyId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("No se usa en las pruebas de ChatService.");

    public Task RecordActivityAsync(Guid childProfileId, Guid familyId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
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
