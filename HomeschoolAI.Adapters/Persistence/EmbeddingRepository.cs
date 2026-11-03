using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector;
using HomeschoolAI.Application.Telemetry;
using HomeschoolAI.Domain.Repositories;
using HomeschoolAI.Domain.Retrieval;

namespace HomeschoolAI.Adapters.Persistence;

/// <summary>
/// Implementación del repositorio de recuperación. Replica la búsqueda híbrida
/// RRF validada en el pipeline Python (k = 60) sobre <c>embeddings_sep</c>.
/// </summary>
public sealed partial class EmbeddingRepository : IEmbeddingRepository
{
    private const int RrfK = 60;

    private const string SqlVector = """
        SELECT id, source, chunk_id, texto, embedding <=> @qv::vector AS score
        FROM embeddings_sep
        ORDER BY embedding <=> @qv::vector
        LIMIT @limit
        """;

    private const string SqlKeyword = """
        WITH q AS (SELECT to_tsquery('spanish', @tsq) AS tsq)
        SELECT e.id, e.source, e.chunk_id, e.texto, ts_rank_cd(e.fts, q.tsq) AS score
        FROM embeddings_sep e, q
        WHERE e.fts @@ q.tsq
        ORDER BY score DESC
        LIMIT @limit
        """;

    private const string SqlHybrid = """
        WITH q AS (SELECT to_tsquery('spanish', @tsq) AS tsq),
        semantic AS (
            SELECT id, ROW_NUMBER() OVER (ORDER BY dist) AS rnk
            FROM (SELECT id, embedding <=> @qv::vector AS dist
                  FROM embeddings_sep
                  ORDER BY embedding <=> @qv::vector
                  LIMIT @pool) t
        ),
        keyword AS (
            SELECT id, ROW_NUMBER() OVER (ORDER BY score DESC) AS rnk
            FROM (SELECT e.id, ts_rank_cd(e.fts, q.tsq) AS score
                  FROM embeddings_sep e, q
                  WHERE e.fts @@ q.tsq
                  ORDER BY score DESC
                  LIMIT @pool) t
        ),
        fused AS (
            SELECT id,
                   COALESCE(1.0 / (@k + s.rnk), 0) + COALESCE(1.0 / (@k + k.rnk), 0) AS rrf
            FROM semantic s FULL OUTER JOIN keyword k USING (id)
        )
        SELECT e.id, e.source, e.chunk_id, e.texto, f.rrf::float8 AS score
        FROM fused f JOIN embeddings_sep e USING (id)
        ORDER BY f.rrf DESC, e.id
        LIMIT @limit
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<EmbeddingRepository> _logger;

    public EmbeddingRepository(NpgsqlDataSource dataSource, ILogger<EmbeddingRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<SearchOutcome> SearchAsync(
        Vector? queryVector,
        string question,
        SearchMode mode,
        int limit,
        int pool,
        CancellationToken cancellationToken = default)
    {
        using var activity = ApplicationTelemetry.ActivitySource.StartActivity("retrieval");
        activity?.SetTag("search.mode", mode.ToString());

        var tsQuery = BuildTsQuery(question);
        var degraded = false;

        if ((mode is SearchMode.Vector or SearchMode.Hybrid) && queryVector is null)
        {
            throw new ArgumentException(
                "El modo vectorial/híbrido requiere el vector de la consulta.",
                nameof(queryVector));
        }

        if ((mode is SearchMode.Keyword or SearchMode.Hybrid) && tsQuery.Length == 0)
        {
            if (mode == SearchMode.Keyword)
            {
                return new SearchOutcome(Array.Empty<RetrievalResult>(), Degraded: false);
            }

            // Sin términos útiles: solo vectorial (igual que el pipeline Python).
            mode = SearchMode.Vector;
            degraded = true;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        switch (mode)
        {
            case SearchMode.Vector:
                command.CommandText = SqlVector;
                command.Parameters.AddWithValue("@qv", queryVector!);
                command.Parameters.AddWithValue("@limit", limit);
                break;

            case SearchMode.Keyword:
                command.CommandText = SqlKeyword;
                command.Parameters.AddWithValue("@tsq", tsQuery);
                command.Parameters.AddWithValue("@limit", limit);
                break;

            case SearchMode.Hybrid:
                command.CommandText = SqlHybrid;
                command.Parameters.AddWithValue("@tsq", tsQuery);
                command.Parameters.AddWithValue("@qv", queryVector!);
                command.Parameters.AddWithValue("@limit", limit);
                command.Parameters.AddWithValue("@pool", Math.Max(pool, limit));
                command.Parameters.AddWithValue("@k", RrfK);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Modo de búsqueda no soportado.");
        }

        var results = new List<RetrievalResult>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new RetrievalResult(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetDouble(4)));
            }

            stopwatch.Stop();
            LogSqlExecuted(_logger, mode, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogSqlError(_logger, mode, stopwatch.ElapsedMilliseconds, ex);
            throw;
        }

        activity?.SetTag("search.results", results.Count);

        return new SearchOutcome(results, degraded);
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug,
        Message = "Consulta SQL {Mode} ejecutada en {ElapsedMs}ms")]
    private static partial void LogSqlExecuted(ILogger logger, SearchMode mode, long elapsedMs);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error,
        Message = "Error ejecutando la consulta SQL de búsqueda {Mode} tras {ElapsedMs}ms")]
    private static partial void LogSqlError(ILogger logger, SearchMode mode, long elapsedMs, Exception exception);

    /// <summary>
    /// Términos de la pregunta unidos con OR. Solo caracteres de palabra (>= 3),
    /// sin riesgo de inyección en to_tsquery.
    /// </summary>
    private static string BuildTsQuery(string question)
    {
        var terms = Regex.Matches(question.ToLowerInvariant(), @"\w{3,}")
            .Select(m => m.Value)
            .Distinct();

        return string.Join(" | ", terms);
    }
}
