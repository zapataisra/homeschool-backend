namespace HomeschoolAI.Domain.Conversation;

/// <summary>
/// Historial completo de una conversación identificada por <see cref="ThreadId"/>.
/// </summary>
public sealed record ConversationHistory(string ThreadId, IReadOnlyList<ConversationMessage> Messages);
