namespace HomeschoolAI.Domain.Retrieval;

/// <summary>
/// Resultado de una búsqueda (vectorial, léxica o híbrida).
/// </summary>
public sealed record RetrievalResult(
    int Id,
    string Source,
    int ChunkId,
    string Texto,
    double Score);
