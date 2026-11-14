namespace HomeschoolAI.Domain.Conversation;

/// <summary>
/// Puerto de salida: orquestación del agente Axolotl (LLM + herramientas).
/// La implementación con Semantic Kernel vive en Adapters; el dominio no
/// conoce Semantic Kernel ni OpenAI.
/// </summary>
public interface IAgentCompletionService
{
    /// <summary>
    /// Genera la respuesta del asistente para el historial dado (que ya incluye
    /// el último mensaje del usuario) usando el mensaje de sistema indicado.
    /// Devuelve únicamente el texto final del asistente.
    /// </summary>
    Task<string> CompleteAsync(ConversationHistory history, string systemPrompt, CancellationToken cancellationToken = default);
}
