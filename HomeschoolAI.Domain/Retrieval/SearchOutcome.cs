namespace HomeschoolAI.Domain.Retrieval;

/// <summary>
/// Resultado de una búsqueda junto con el indicador de degradación
/// (híbrido → vectorial por falta de términos útiles).
/// </summary>
public sealed record SearchOutcome(IReadOnlyList<RetrievalResult> Items, bool Degraded);
