namespace HomeschoolAI.Domain.Retrieval;

/// <summary>
/// Estrategia de recuperación: vectorial, léxica o híbrida (RRF).
/// </summary>
public enum SearchMode
{
    Vector = 0,
    Keyword = 1,
    Hybrid = 2
}
