namespace HomeschoolAI.Domain.Embedding;

/// <summary>
/// Puerto de salida: genera el vector de consulta (embedding) con el modelo
/// guardado en <c>embedding_model</c> (all-MiniLM-L6-v2, 384 dimensiones).
/// </summary>
public interface IQueryEmbedder
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
