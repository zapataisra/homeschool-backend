namespace HomeschoolAI.Application.Chat;

/// <summary>
/// Petición de chat: el mensaje del usuario y, opcionalmente, el hilo de conversación.
/// </summary>
public sealed class ChatRequest
{
    public string? ThreadId { get; init; }

    public string Message { get; init; } = string.Empty;
}
