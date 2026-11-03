using HomeschoolAI.Application.Chat;

namespace HomeschoolAI.Application.Ports;

/// <summary>
/// Puerto de entrada: conversación con el agente LUMI.
/// </summary>
public interface IChatService
{
    Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
