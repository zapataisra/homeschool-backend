using Pgvector;
using HomeschoolAI.Domain.Retrieval;

namespace HomeschoolAI.Domain.Repositories;

/// <summary>
/// Puerto de salida: recuperación de fragmentos curriculares desde PostgreSQL/pgvector.
/// </summary>
public interface IEmbeddingRepository
{
    Task<SearchOutcome> SearchAsync(
        Vector? queryVector,
        string question,
        SearchMode mode,
        int limit,
        int pool,
        CancellationToken cancellationToken = default);
}
