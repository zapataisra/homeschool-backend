using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pgvector;
using HomeschoolAI.Application.Dtos;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Domain.Embedding;
using HomeschoolAI.Domain.Repositories;
using HomeschoolAI.Domain.Retrieval;

namespace HomeschoolAI.Application.Search;

/// <summary>
/// Caso de uso de búsqueda: orquesta el puerto de salida (repositorio)
/// y traduce los resultados del dominio a DTOs de aplicación.
/// </summary>
public sealed partial class SearchService : ISearchService
{
    private readonly IEmbeddingRepository _repository;
    private readonly IQueryEmbedder _embedder;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IEmbeddingRepository repository, IQueryEmbedder embedder, ILogger<SearchService> logger)
    {
        _repository = repository;
        _embedder = embedder;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchChunkDto>> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        Vector? queryVector = null;
        long vectorizationMs = 0;

        if (query.Mode is SearchMode.Vector or SearchMode.Hybrid)
        {
            var vectorStopwatch = Stopwatch.StartNew();
            var embedding = await _embedder.EmbedAsync(query.Question, cancellationToken);
            vectorStopwatch.Stop();
            vectorizationMs = vectorStopwatch.ElapsedMilliseconds;
            queryVector = new Vector(embedding);
        }

        var stopwatch = Stopwatch.StartNew();
        var outcome = await _repository.SearchAsync(
            queryVector,
            query.Question,
            query.Mode,
            query.Limit,
            query.Pool,
            cancellationToken);
        stopwatch.Stop();

        LogSearchCompleted(
            _logger,
            query.Mode,
            outcome.Items.Count,
            stopwatch.ElapsedMilliseconds,
            vectorizationMs,
            query.Question.Length,
            outcome.Degraded);

        if (outcome.Degraded)
        {
            LogSearchDegraded(_logger, query.Mode);
        }

        return outcome.Items
            .Select(r => new SearchChunkDto(r.Id, r.Source, r.ChunkId, r.Texto, r.Score))
            .ToList();
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Búsqueda completada: modo {Mode}, resultados {ResultCount}, duración {ElapsedMs}ms, vectorización {VectorizationMs}ms, longitud de pregunta {QuestionLength}, degradada {Degraded}")]
    private static partial void LogSearchCompleted(
        ILogger logger, SearchMode mode, int resultCount, long elapsedMs, long vectorizationMs, int questionLength, bool degraded);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "Búsqueda degradada a solo vectorial (sin términos útiles): modo original {Mode}")]
    private static partial void LogSearchDegraded(ILogger logger, SearchMode mode);
}
