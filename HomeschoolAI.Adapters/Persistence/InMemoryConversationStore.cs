using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Domain.Conversation;

namespace HomeschoolAI.Adapters.Persistence;

/// <summary>
/// Almacén en memoria del historial de conversación (dentro del servidor).
/// No es distribuido: está pensado para el despliegue actual de una sola instancia.
/// Implementa el puerto <see cref="IConversationStore"/>, por lo que puede
/// sustituirse por Redis/otro almacén compartido sin tocar el caso de uso.
/// </summary>
public sealed partial class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, Entry> _threads = new(StringComparer.Ordinal);
    private readonly TimeSpan _idleTimeout;
    private readonly int _maxThreads;
    private readonly ILogger<InMemoryConversationStore> _logger;

    public InMemoryConversationStore(
        ILogger<InMemoryConversationStore> logger,
        TimeSpan? idleTimeout = null,
        int maxThreads = 1000)
    {
        _logger = logger;
        _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(20);
        _maxThreads = maxThreads;
    }

    public Task<ConversationHistory> GetOrCreateAsync(string userId, string threadId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = Key(userId, threadId);

        if (_threads.TryGetValue(key, out var entry))
        {
            if (DateTimeOffset.UtcNow - entry.LastAccessed > _idleTimeout)
            {
                _threads.TryRemove(key, out _);
                LogThreadExpired(_logger, threadId.Length);
                return Task.FromResult(new ConversationHistory(threadId, Array.Empty<ConversationMessage>()));
            }

            entry.LastAccessed = DateTimeOffset.UtcNow;
            return Task.FromResult(entry.History);
        }

        return Task.FromResult(new ConversationHistory(threadId, Array.Empty<ConversationMessage>()));
    }

    public Task SaveAsync(string userId, ConversationHistory history, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = Key(userId, history.ThreadId);

        if (!_threads.ContainsKey(key) && _threads.Count >= _maxThreads)
        {
            EvictOldest();
        }

        _threads[key] = new Entry(history, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    private static string Key(string userId, string threadId) => $"{userId}:{threadId}";

    private void EvictOldest()
    {
        var oldest = _threads.OrderBy(kvp => kvp.Value.LastAccessed).FirstOrDefault();
        if (oldest.Key is not null)
        {
            _threads.TryRemove(oldest.Key, out _);
            LogEvicted(_logger, oldest.Key.Length);
        }
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "Conversación expirada por inactividad (longitud de hilo {ThreadIdLength})")]
    private static partial void LogThreadExpired(ILogger logger, int threadIdLength);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Warning,
        Message = "Caché de conversaciones llena: se evictó el hilo con acceso más antiguo (longitud {ThreadIdLength})")]
    private static partial void LogEvicted(ILogger logger, int threadIdLength);

    private sealed class Entry
    {
        public Entry(ConversationHistory history, DateTimeOffset lastAccessed)
        {
            History = history;
            LastAccessed = lastAccessed;
        }

        public ConversationHistory History { get; }

        public DateTimeOffset LastAccessed { get; set; }
    }
}
