namespace HomeschoolAI.Domain.Conversation;

/// <summary>
/// Rol de un mensaje persistido en el historial de una conversación con Axolotl.
/// Solo se persisten los turnos de usuario y asistente; el mensaje de sistema
/// y las llamadas a herramientas son efímeros (se reconstruyen en cada petición).
/// </summary>
public enum ConversationRole
{
    User = 0,
    Assistant = 1
}
