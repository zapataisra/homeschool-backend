using System.Text.Json;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Domain.Conversation;
using HomeschoolAI.Domain.Progress;
using HomeschoolAI.Domain.Validation;

namespace HomeschoolAI.Application.Evaluation;

/// <summary>
/// Orquestador del Agente Crítico: pide al LLM extraer la operación y evaluar la
/// estrategia, valida la matemática de forma determinista (<see cref="IMathValidator"/>)
/// y calcula los XP (<see cref="IXPCalculator"/>). El LLM nunca decide si la operación
/// es correcta: eso lo decide el código.
/// </summary>
public sealed class CriticalEvaluationService
{
    private const string SystemInstruction =
        "Eres el Agente Crítico de LUMI, un evaluador matemático. Analiza la respuesta de la niña y devuelve " +
        "ESTRICTAMENTE un objeto JSON con este esquema (sin texto adicional, sin comillas de markdown):\n" +
        "{ \"precision\": 0-100, \"confidence\": 0.0-1.0, \"missingConcepts\": [\"...\"], " +
        "\"extractedOperation\": { \"operand1\": numero, \"operation\": \"+\"|\"-\"|\"*\"|\"/\", \"operand2\": numero, \"proposedResult\": numero }, " +
        "\"sourceChunkIds\": [numeros] }";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMathValidator _mathValidator;
    private readonly IXPCalculator _xpCalculator;
    private readonly IAgentCompletionService _agentCompletion;
    private readonly ILogger<CriticalEvaluationService> _logger;

    public CriticalEvaluationService(
        IMathValidator mathValidator,
        IXPCalculator xpCalculator,
        IAgentCompletionService agentCompletion,
        ILogger<CriticalEvaluationService> logger)
    {
        _mathValidator = mathValidator;
        _xpCalculator = xpCalculator;
        _agentCompletion = agentCompletion;
        _logger = logger;
    }

    public async Task<CriticalEvaluationResult> EvaluateAsync(
        string childStrategy,
        int attempt,
        int baseXp,
        CancellationToken cancellationToken = default)
    {
        var response = await _agentCompletion.CompleteAsync(BuildHistory(childStrategy), SystemInstruction, cancellationToken);

        EvaluationResultDto? evaluation;
        try
        {
            evaluation = JsonSerializer.Deserialize<EvaluationResultDto>(response, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("El Agente Crítico devolvió JSON inválido: {Message}", ex.Message);
            return new CriticalEvaluationResult(null, MathIsCorrect: false, XpAwarded: 0);
        }

        if (evaluation is null)
        {
            return new CriticalEvaluationResult(null, MathIsCorrect: false, XpAwarded: 0);
        }

        // La matemática la valida el código (determinista), nunca el LLM.
        var mathIsCorrect = evaluation.ExtractedOperation is { } op
            && _mathValidator.ValidateOperation(op.Operand1, op.Operation, op.Operand2, op.ProposedResult);

        var xp = mathIsCorrect ? _xpCalculator.CalculateXp(baseXp, attempt) : 0;

        return new CriticalEvaluationResult(evaluation, mathIsCorrect, xp);
    }

    private static ConversationHistory BuildHistory(string childStrategy)
    {
        var message = $"Respuesta de la niña:\n\"{childStrategy}\"";
        return new ConversationHistory(
            Guid.NewGuid().ToString("N"),
            new[] { new ConversationMessage(ConversationRole.User, message) });
    }
}
