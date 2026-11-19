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
/// es correcta: eso lo decide el código (Paso A determinista).
/// </summary>
public sealed class CriticalEvaluationService
{
    private const string SystemInstruction =
        "Eres el Agente Crítico de la plataforma Escuela en casa MX, un evaluador pedagógico para niños de primaria (6 a 12 años). Analiza la respuesta de la niña contra el reto planteado, la última pregunta del tutor y los fragmentos oficiales de la SEP, y devuelve " +
        "ESTRICTAMENTE un objeto JSON con este esquema (sin texto adicional, sin comillas de markdown):\n" +
        "{ \"precision\": 0-100, \"confidence\": 0.0-1.0, \"missingConcepts\": [\"...\"], " +
        "\"reformulationHint\": \"cómo re-explicar sin dar la respuesta\", " +
        "\"extractedOperation\": { \"expression\": \"expresión matemática completa si la hay, si no devuelve todo el objeto en null\", \"operand1\": numero, \"operation\": \"+\"|\"-\"|\"*\"|\"/\", \"operand2\": numero, \"proposedResult\": numero }, " +
        "\"sourceChunkIds\": [numeros] }\n" +
        "\n" +
        "RÚBRICA DE PRECISIÓN (Paso B, pensada para primaria):\n" +
        "- Si la niña responde de forma coherente a la pregunta de Axolotl e identifica correctamente el concepto o un ejemplo válido con SUS PROPIAS palabras (p. ej. nombrar un derecho real —educación, salud, juego o familia— y dar una razón sencilla), asigna precision >= 80 (aprobado).\n" +
        "- NO exijas definiciones académicas textuales, tecnicismos ni longitud de ensayo: valora la comprensión real, no la memorización.\n" +
        "- Reserva precision < 70 solo cuando la respuesta sea incoherente, no responda a la pregunta o mencione un concepto equivocado.\n" +
        "\n" +
        "PASO A (matemáticas, determinista):\n" +
        "- Extrae una operación/expresión SOLO si la respuesta contiene cálculos o comparaciones numéricas (p. ej. \"4*3=12\", \"2/4 > 3/8\").\n" +
        "- Si el reto y la respuesta NO involucran matemáticas (Ética, Lenguajes, etc.), devuelve \"extractedOperation\": null (el objeto completo en null, NO un objeto con campos vacíos); el sistema tratará el Paso A como válido automáticamente, sin falsos negativos.";

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

    /// <summary>Evalúa sin contexto curricular (flujo del endpoint de evaluación).</summary>
    public Task<CriticalEvaluationResult> EvaluateAsync(
        string childStrategy,
        int attempt,
        int baseXp,
        CancellationToken cancellationToken = default)
        => EvaluateAsync(childStrategy, attempt, baseXp, challengeContext: null, cancellationToken);

    /// <summary>Evalúa la respuesta del niño contra el reto del tema y los fragmentos SEP recuperados.</summary>
    public async Task<CriticalEvaluationResult> EvaluateAsync(
        string childStrategy,
        int attempt,
        int baseXp,
        string? challengeContext,
        CancellationToken cancellationToken = default)
    {
        var response = await _agentCompletion.CompleteAsync(BuildHistory(childStrategy, challengeContext), SystemInstruction, cancellationToken);

        EvaluationResultDto? evaluation;
        try
        {
            evaluation = JsonSerializer.Deserialize<EvaluationResultDto>(response, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("El Agente Crítico devolvió JSON inválido: {Message}", ex.Message);
            return Failed();
        }

        if (evaluation is null)
        {
            return Failed();
        }

        // La matemática la valida el código (determinista), nunca el LLM.
        var (mathIsCorrect, stepAValid) = EvaluateStepA(evaluation.ExtractedOperation);

        // Capa determinista adicional (Paso A): fracciones descritas en lenguaje natural.
        // Si el niño propuso fracciones que NO son equivalentes (p. ej. "8/24 = 8/12"),
        // el intento queda invalidado sin importar la precisión que asigne el LLM.
        if (_mathValidator.ValidateFractionEquivalence(childStrategy) == false)
        {
            mathIsCorrect = false;
            stepAValid = false;
        }

        var xp = mathIsCorrect ? _xpCalculator.CalculateXp(baseXp, attempt) : 0;

        return new CriticalEvaluationResult(evaluation, mathIsCorrect, xp, stepAValid);
    }

    /// <summary>
    /// Paso A: devuelve si la operación extraída es correcta y si el Paso A queda
    /// superado. Si no hubo matemáticas, el Paso A no aplica y se considera válido.
    /// </summary>
    private (bool MathIsCorrect, bool StepAValid) EvaluateStepA(ExtractedOperationDto? operation)
    {
        if (operation is null)
        {
            return (MathIsCorrect: false, StepAValid: true);
        }

        if (!string.IsNullOrWhiteSpace(operation.Expression))
        {
            var expressionValid = _mathValidator.ValidateExpression(operation.Expression);
            return (expressionValid, expressionValid);
        }

        var operationValid = _mathValidator.ValidateOperation(operation.Operand1, operation.Operation, operation.Operand2, operation.ProposedResult);
        return (operationValid, operationValid);
    }

    private static CriticalEvaluationResult Failed() =>
        new(Evaluation: null, MathIsCorrect: false, XpAwarded: 0, StepAValid: false);

    private static ConversationHistory BuildHistory(string childStrategy, string? challengeContext)
    {
        var message = string.IsNullOrWhiteSpace(challengeContext)
            ? $"Respuesta de la niña:\n\"{childStrategy}\""
            : $"{challengeContext}\n\nRespuesta de la niña:\n\"{childStrategy}\"";

        return new ConversationHistory(
            Guid.NewGuid().ToString("N"),
            new[] { new ConversationMessage(ConversationRole.User, message) });
    }
}
