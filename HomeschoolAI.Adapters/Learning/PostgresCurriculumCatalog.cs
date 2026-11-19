using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Npgsql;
using HomeschoolAI.Application.Learning;

namespace HomeschoolAI.Adapters.Learning;

/// <summary>
/// Catálogo curricular sobre <c>public.curriculum_topics</c> (Npgsql, solo lectura).
/// Si la tabla está vacía o la base de datos no responde, respalda con
/// <see cref="SeedCurriculumCatalog"/> para no romper el arranque ni las pruebas.
/// </summary>
public sealed class PostgresCurriculumCatalog : ICurriculumCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // TTL largo para resultados reales; TTL corto para el respaldo estático, de modo
    // que un fallo de BD no deje datos degradados en caché demasiado tiempo.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FallbackTtl = TimeSpan.FromSeconds(30);

    private const string GradeSql = """
        SELECT topic_id, grade, trimester, order_index, title, campo_formativo,
               search_query, weekly_project_title, weekly_project_goal,
               prerequisite_topic_id, blocks_json
        FROM public.curriculum_topics
        WHERE grade = $1 AND is_active = true
        ORDER BY order_index;
        """;

    private const string FindSql = """
        SELECT topic_id, grade, trimester, order_index, title, campo_formativo,
               search_query, weekly_project_title, weekly_project_goal,
               prerequisite_topic_id, blocks_json
        FROM public.curriculum_topics
        WHERE topic_id = $1 AND is_active = true;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PostgresCurriculumCatalog> _logger;

    public PostgresCurriculumCatalog(NpgsqlDataSource dataSource, IMemoryCache cache, ILogger<PostgresCurriculumCatalog> logger)
    {
        _dataSource = dataSource;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SeedTopic>> GetTopicsForGradeAsync(int grade, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"curriculum:grade:{grade}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<SeedTopic>? cached) && cached is not null)
        {
            return cached;
        }

        IReadOnlyList<SeedTopic> topics;
        TimeSpan ttl;

        try
        {
            var fromDb = await QueryListAsync(GradeSql, grade, cancellationToken);
            if (fromDb.Count > 0)
            {
                topics = fromDb;
                ttl = CacheTtl;
            }
            else
            {
                _logger.LogWarning("public.curriculum_topics no tiene temas para el grado {Grade}; usando el catálogo estático.", grade);
                topics = SeedCurriculumCatalog.GetTopicsForGrade(grade);
                ttl = FallbackTtl;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar public.curriculum_topics; usando el catálogo estático.");
            topics = SeedCurriculumCatalog.GetTopicsForGrade(grade);
            ttl = FallbackTtl;
        }

        _cache.Set(cacheKey, topics, ttl);
        return topics;
    }

    public async Task<SeedTopic?> FindAsync(string topicId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"curriculum:topic:{topicId}";
        if (_cache.TryGetValue(cacheKey, out SeedTopic? cached))
        {
            return cached;
        }

        SeedTopic? topic;
        TimeSpan ttl;

        try
        {
            topic = await QuerySingleAsync(FindSql, topicId, cancellationToken);
            if (topic is not null)
            {
                ttl = CacheTtl;
            }
            else
            {
                topic = SeedCurriculumCatalog.TryFind(topicId);
                ttl = FallbackTtl;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar public.curriculum_topics para '{TopicId}'; usando el catálogo estático.", topicId);
            topic = SeedCurriculumCatalog.TryFind(topicId);
            ttl = FallbackTtl;
        }

        if (topic is not null)
        {
            _cache.Set(cacheKey, topic, ttl);
        }

        return topic;
    }

    private async Task<List<SeedTopic>> QueryListAsync(string sql, object param, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(param);

        var result = new List<SeedTopic>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapRow(reader));
        }

        return result;
    }

    private async Task<SeedTopic?> QuerySingleAsync(string sql, object param, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(param);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
    }

    private static SeedTopic MapRow(NpgsqlDataReader reader) => new(
        reader.GetInt32(1),                       // grade
        reader.GetInt32(2),                       // trimester
        reader.GetInt32(3),                       // order_index
        reader.GetString(0),                      // topic_id
        reader.GetString(4),                      // title
        reader.GetString(5),                      // campo_formativo
        reader.GetString(6),                      // search_query
        reader.GetString(7),                      // weekly_project_title
        reader.GetString(8),                      // weekly_project_goal
        reader.IsDBNull(9) ? null : reader.GetString(9), // prerequisite_topic_id
        DeserializeBlocks(reader.GetString(10))); // blocks_json

    private static IReadOnlyList<LessonBlockDto> DeserializeBlocks(string json)
    {
        var blocks = JsonSerializer.Deserialize<List<LessonBlockDto>>(json, JsonOptions);
        return blocks ?? new List<LessonBlockDto>();
    }
}