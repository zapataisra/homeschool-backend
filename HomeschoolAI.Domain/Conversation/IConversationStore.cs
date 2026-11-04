namespace HomeschoolAI.Domain.Conversation;

/// <summary>
/// Puerto de salida: persistencia del historial de conversación.
/// La implementación en memoria vive en Adapters; puede sustituirse por un
/// almacén distribuido (p. ej. Redis) sin tocar el caso de uso.
/// </summary>
public interface IConversationStore
{
    Task<ConversationHistory> GetOrCreateAsync(string userId, string threadId, CancellationToken cancellationToken = default);

    Task SaveAsync(string userId, ConversationHistory history, CancellationToken cancellationToken = default);
}
