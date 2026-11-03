namespace HomeschoolAI.Application.Chat;

/// <summary>
/// Respuesta del agente: el hilo de conversación (para continuar el diálogo) y la respuesta.
/// </summary>
public sealed record ChatResponse(string ThreadId, string Content);
