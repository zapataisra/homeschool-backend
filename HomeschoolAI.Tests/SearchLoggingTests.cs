using Microsoft.Extensions.Logging;
using Pgvector;
using HomeschoolAI.Application.Search;
using HomeschoolAI.Domain.Embedding;
using HomeschoolAI.Domain.Repositories;
using HomeschoolAI.Domain.Retrieval;
using Xunit;

namespace HomeschoolAI.Tests;

public class SearchLoggingTests
{
    [Fact]
    public async Task Search_does_not_log_question_text_or_fragment_content()
    {
        const string secretQuestion = "PREGUNTA_SECRETA_XYZ_123";
        const string secretFragment = "FRAGMENTO_SECRETO_ABC_456";

        var sink = new LogSink();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(sink));

        var repository = new FakeEmbeddingRepository(new SearchOutcome(
            new[] { new RetrievalResult(1, "fuente.pdf", 1, secretFragment, 0.5) },
            Degraded: false));
        var embedder = new FakeQueryEmbedder(new float[384]);

        var service = new SearchService(repository, embedder, loggerFactory.CreateLogger<SearchService>());

        await service.SearchAsync(new SearchQuery
        {
            Question = secretQuestion,
            Mode = SearchMode.Hybrid
        });

        var allLogs = string.Join('\n', sink.Messages);

        Assert.NotEmpty(sink.Messages);
        Assert.Contains("Búsqueda completada", allLogs);
        Assert.Contains(secretQuestion.Length.ToString(), allLogs);
        Assert.DoesNotContain(secretQuestion, allLogs);
        Assert.DoesNotContain(secretFragment, allLogs);
    }

    private sealed class FakeEmbeddingRepository : IEmbeddingRepository
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

    private sealed class FakeQueryEmbedder : IQueryEmbedder
    {
        private readonly float[] _vector;

        public FakeQueryEmbedder(float[] vector) => _vector = vector;

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(_vector);
    }

    private sealed class LogSink : ILoggerProvider
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

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _messages.Add(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
