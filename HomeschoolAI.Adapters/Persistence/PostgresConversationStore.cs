using Microsoft.EntityFrameworkCore;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Domain.Conversation;

namespace HomeschoolAI.Adapters.Persistence;

/// <summary>
/// Almacén del historial de conversación en PostgreSQL (tabla
/// <c>app_identity.conversation_messages</c>). Sustituye al almacén en memoria:
/// el historial sobrevive a recargas y reinicios del proceso.
/// </summary>
public sealed class PostgresConversationStore : IConversationStore
{
    private readonly AppDbContext _db;

    public PostgresConversationStore(AppDbContext db) => _db = db;

    public async Task<ConversationHistory> GetOrCreateAsync(string userId, string threadId, CancellationToken cancellationToken = default)
    {
        var sessionId = SessionKey(userId, threadId);

        var rows = await _db.ConversationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var messages = rows
            .Select(m => new ConversationMessage(ParseRole(m.Role), m.Content))
            .ToList();

        return new ConversationHistory(threadId, messages);
    }

    public async Task SaveAsync(string userId, ConversationHistory history, CancellationToken cancellationToken = default)
    {
        var sessionId = SessionKey(userId, history.ThreadId);

        // Reemplaza la sesión completa (misma semántica que el almacén en memoria).
        var existing = await _db.ConversationMessages
            .Where(m => m.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        _db.ConversationMessages.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var index = 0;
        foreach (var message in history.Messages)
        {
            _db.ConversationMessages.Add(new ConversationMessageEntity
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = message.Role == ConversationRole.User ? "user" : "assistant",
                Content = message.Content,
                CreatedAtUtc = now.AddTicks(index++)
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAsync(string userId, string threadId, CancellationToken cancellationToken = default)
    {
        var sessionId = SessionKey(userId, threadId);

        var existing = await _db.ConversationMessages
            .Where(m => m.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        _db.ConversationMessages.RemoveRange(existing);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string SessionKey(string userId, string threadId) => $"{userId}:{threadId}";

    private static ConversationRole ParseRole(string role) =>
        role == "user" ? ConversationRole.User : ConversationRole.Assistant;
}