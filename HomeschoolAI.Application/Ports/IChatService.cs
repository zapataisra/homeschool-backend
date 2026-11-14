using HomeschoolAI.Application.Chat;
using HomeschoolAI.Application.Learning;

namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Puerto de entrada: conversación con el agente Axolotl.
/// </summary>
public interface IChatService
{
    Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>Inicia la sesión de aprendizaje proactiva del niño (tema del día + saludo de Axolotl).</summary>
    Task<LearningSessionResponse> StartLearningSessionAsync(CancellationToken cancellationToken = default);
}
