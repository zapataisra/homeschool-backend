namespace HomeschoolAI.Application.Evaluation;

/// <summary>
/// Resultado de la evaluación del Agente Crítico (EvaluationJSON).
/// </summary>
public sealed class EvaluationResultDto
{
    /// <summary>Precisión 0-100.</summary>
    public int Precision { get; init; }

    /// <summary>Nivel de confianza 0.0-1.0.</summary>
    public double Confidence { get; init; }

    /// <summary>Conceptos faltantes detectados en la estrategia de la niña.</summary>
    public List<string> MissingConcepts { get; init; } = new();

    /// <summary>Operación aritmética extraída de la respuesta (lista para validar).</summary>
    public ExtractedOperationDto? ExtractedOperation { get; init; }

    /// <summary>IDs de los fragmentos de la SEP utilizados por el evaluador.</summary>
    public List<int> SourceChunkIds { get; init; } = new();
}

/// <summary>Operación aritmética extraída por el LLM, para validar con <c>IMathValidator</c>.</summary>
public sealed class ExtractedOperationDto
{
    public decimal Operand1 { get; init; }

    public string Operation { get; init; } = string.Empty;

    public decimal Operand2 { get; init; }

    public decimal ProposedResult { get; init; }
}

/// <summary>Resultado del orquestador: evaluación + validación matemática determinista + XP.</summary>
public sealed record CriticalEvaluationResult(
    EvaluationResultDto? Evaluation,
    bool MathIsCorrect,
    int XpAwarded);
