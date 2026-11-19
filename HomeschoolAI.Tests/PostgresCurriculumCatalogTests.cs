using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using HomeschoolAI.Adapters.Learning;
using Xunit;

namespace HomeschoolAI.Tests;

public class PostgresCurriculumCatalogTests
{
    private static MemoryCache CreateCache() => new(new MemoryCacheOptions());

    private static NpgsqlDataSource CreateUnreachableDataSource()
    {
        // Conexión a un puerto sin servicio: la consulta falla y debe respaldar con el catálogo estático.
        var builder = new NpgsqlDataSourceBuilder(
            "Host=127.0.0.1;Port=1;Database=vectordb;Username=app_readonly;Password=x;Timeout=2");
        return builder.Build();
    }

    [Fact]
    public async Task Fallback_returns_static_topics_when_db_unavailable()
    {
        await using var dataSource = CreateUnreachableDataSource();
        var catalog = new PostgresCurriculumCatalog(dataSource, CreateCache(), NullLogger<PostgresCurriculumCatalog>.Instance);

        var topics = await catalog.GetTopicsForGradeAsync(4);

        Assert.True(topics.Count >= 12, $"El fallback estático debe devolver >= 12 temas; devolvió {topics.Count}.");

        var topic = await catalog.FindAsync("fase4-saberes-repartos");
        Assert.NotNull(topic);
        Assert.Equal(3, topic.Blocks.Count);
    }

    [Fact]
    public async Task Cache_serves_repeated_grade_calls_within_ttl()
    {
        await using var dataSource = CreateUnreachableDataSource();
        var cache = CreateCache();
        var catalog = new PostgresCurriculumCatalog(dataSource, cache, NullLogger<PostgresCurriculumCatalog>.Instance);

        var first = await catalog.GetTopicsForGradeAsync(4);
        var second = await catalog.GetTopicsForGradeAsync(4);

        // Misma instancia => el segundo resultado salió del caché, no de un re-query + fallback.
        Assert.Same(first, second);
        Assert.True(cache.TryGetValue("curriculum:grade:4", out _));
    }

    [Fact]
    public async Task Cache_serves_repeated_find_calls_within_ttl()
    {
        await using var dataSource = CreateUnreachableDataSource();
        var cache = CreateCache();
        var catalog = new PostgresCurriculumCatalog(dataSource, cache, NullLogger<PostgresCurriculumCatalog>.Instance);

        var first = await catalog.FindAsync("fase4-saberes-repartos");

        Assert.NotNull(first);
        Assert.True(cache.TryGetValue("curriculum:topic:fase4-saberes-repartos", out _));
    }

    [Fact]
    public async Task Integration_queries_curriculum_topics_table()
    {
        var builder = new NpgsqlDataSourceBuilder(TestConfiguration.GetConnectionString());
        await using var dataSource = builder.Build();
        var catalog = new PostgresCurriculumCatalog(dataSource, CreateCache(), NullLogger<PostgresCurriculumCatalog>.Instance);

        var grade4 = await catalog.GetTopicsForGradeAsync(4);
        Assert.True(grade4.Count >= 12, $"Grado 4 debe tener >= 12 temas en public.curriculum_topics; tiene {grade4.Count}.");

        var topic = await catalog.FindAsync("fase4-saberes-repartos");
        Assert.NotNull(topic);
        Assert.Equal("Repartos y fracciones equivalentes", topic.Title);
        Assert.Equal(3, topic.Blocks.Count);
        Assert.All(topic.Blocks, b => Assert.StartsWith("/lessons/", b.ImageUrl));
    }
}
