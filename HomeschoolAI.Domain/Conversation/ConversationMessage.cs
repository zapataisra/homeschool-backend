namespace HomeschoolAI.Domain.Conversation;

/// <summary>
/// Mensaje de una conversación (turno de usuario o de asistente).
/// </summary>
public sealed record ConversationMessage(ConversationRole Role, string Content);
