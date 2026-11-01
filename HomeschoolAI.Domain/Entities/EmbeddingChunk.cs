using Pgvector;

namespace HomeschoolAI.Domain.Entities;

/// <summary>
/// Fragmento curricular indexado en la tabla <c>embeddings_sep</c>
/// (Programas Sintéticos de la Nueva Escuela Mexicana).
/// </summary>
public sealed class EmbeddingChunk
{
    public int Id { get; set; }

    public string Source { get; set; } = string.Empty;

    public int ChunkId { get; set; }

    public string Texto { get; set; } = string.Empty;

    public Vector? Embedding { get; set; }

    public string? EmbeddingModel { get; set; }

    public int? Page { get; set; }
}
