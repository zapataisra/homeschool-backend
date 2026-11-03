using Pgvector;
using HomeschoolAI.Domain.Embedding;
using HomeschoolAI.Domain.Repositories;
using HomeschoolAI.Domain.Retrieval;

namespace HomeschoolAI.Tests;

public sealed class FakeEmbeddingRepository : IEmbeddingRepository
{
    private readonly SearchOutcome _outcome;

    public FakeEmbeddingRepository(SearchOutcome outcome) => _outcome = outcome;

    public Task<SearchOutcome> SearchAsync(
        Vector? queryVector,
        string question,
        SearchMode mode,
        int limit,
        int pool,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_outcome);
}

public sealed class FakeQueryEmbedder : IQueryEmbedder
{
    private readonly float[] _vector;

    public FakeQueryEmbedder(float[] vector) => _vector = vector;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(_vector);
}
