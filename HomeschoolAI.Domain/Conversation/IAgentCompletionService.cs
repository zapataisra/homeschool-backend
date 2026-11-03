namespace HomeschoolAI.Domain.Conversation;

/// <summary>
/// Puerto de salida: orquestación del agente LUMI (LLM + herramientas).
/// La implementación con Semantic Kernel vive en Adapters; el dominio no
/// conoce Semantic Kernel ni OpenAI.
/// </summary>
public interface IAgentCompletionService
{
    /// <summary>
    /// Genera la respuesta del asistente para el historial dado (que ya incluye
    /// el último mensaje del usuario). Devuelve únicamente el texto final del asistente.
    /// </summary>
    Task<string> CompleteAsync(ConversationHistory history, CancellationToken cancellationToken = default);
}
