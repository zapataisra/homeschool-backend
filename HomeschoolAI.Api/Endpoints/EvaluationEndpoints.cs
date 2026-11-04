using Microsoft.AspNetCore.Authorization;
using HomeschoolAI.Adapters.Identity;
using HomeschoolAI.Application.Evaluation;

namespace HomeschoolAI.Api.Endpoints;

/// <summary>Endpoint de evaluación del Agente Crítico (rol Child o Guardian).</summary>
public static class EvaluationEndpoints
{
    /// <summary>Base de XP provisional hasta integrar el contexto de nodo/misión.</summary>
    private const int DefaultBaseXp = 100;

    public static void MapEvaluationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/evaluation/evaluate", async (
                EvaluateRequest request,
                CriticalEvaluationService evaluationService,
                CancellationToken cancellationToken) =>
            {
                var text = request.Text?.Trim() ?? string.Empty;

                if (text.Length == 0)
                {
                    return Results.BadRequest(new { error = "El texto no puede estar vacío." });
                }

                if (request.Attempt is < 1 or > 3)
                {
                    return Results.BadRequest(new { error = "El intento debe estar entre 1 y 3." });
                }

                var result = await evaluationService.EvaluateAsync(text, request.Attempt, DefaultBaseXp, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("Evaluate")
            .RequireAuthorization(AuthorizationPolicies.RequireChildOrGuardian);
    }
}

/// <summary>Cuerpo de la petición de evaluación: respuesta de la niña e intento actual.</summary>
public sealed class EvaluateRequest
{
    public string? Text { get; init; }

    public int Attempt { get; init; }
}
