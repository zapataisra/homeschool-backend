using Microsoft.AspNetCore.RateLimiting;
using HomeschoolAI.Application.Chat;
using HomeschoolAI.Application.Ports;

namespace HomeschoolAI.Api.Endpoints;

/// <summary>
/// Endpoint del agente conversacional LUMI (POST /api/agent/chat).
/// El cliente envía solo <c>{ threadId?, message }</c>; el historial de la
/// conversación se conserva en el servidor (nunca se acepta el historial completo).
/// </summary>
public static class AgentEndpoints
{
    private const int MaxMessageLength = 2000;

    public static void MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/agent/chat", async (
                AgentChatRequest request,
                IChatService chatService,
                CancellationToken cancellationToken) =>
            {
                var message = request.Message?.Trim() ?? string.Empty;

                if (message.Length == 0)
                {
                    return Results.BadRequest(new { error = "El mensaje no puede estar vacío." });
                }

                if (message.Length > MaxMessageLength)
                {
                    return Results.BadRequest(new
                    {
                        error = $"El mensaje excede la longitud máxima de {MaxMessageLength} caracteres."
                    });
                }

                var threadId = request.ThreadId?.Trim();
                if (!string.IsNullOrWhiteSpace(threadId) && !IsValidThreadId(threadId))
                {
                    return Results.BadRequest(new { error = "El identificador de conversación no es válido." });
                }

                var response = await chatService.SendAsync(new ChatRequest
                {
                    ThreadId = string.IsNullOrWhiteSpace(threadId) ? null : threadId,
                    Message = message
                }, cancellationToken);

                return Results.Ok(response);
            })
            .WithName("AgentChat")
            .RequireRateLimiting("agent");
    }

    private static bool IsValidThreadId(string value)
    {
        if (value.Length > 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Cuerpo de la petición del endpoint del agente.
/// </summary>
public sealed class AgentChatRequest
{
    public string? ThreadId { get; init; }

    public string? Message { get; init; }
}
