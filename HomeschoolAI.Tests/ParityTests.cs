using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector;
using Pgvector.Npgsql;
using HomeschoolAI.Adapters.Embedding;
using HomeschoolAI.Adapters.Persistence;
using HomeschoolAI.Domain.Retrieval;
using Xunit;
using Xunit.Abstractions;

namespace HomeschoolAI.Tests;

public class ParityTests
{
    private readonly ITestOutputHelper _output;

    public ParityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Hybrid_search_matches_python_ids_and_embedding()
    {
        var parityPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "parity_data.json"));

        if (!File.Exists(parityPath))
        {
            _output.WriteLine("SKIPPED: no existe parity_data.json (ejecuta 'python parity_check.py' primero).");
            return;
        }

        var cases = JsonSerializer.Deserialize<List<ParityCase>>(
            await File.ReadAllTextAsync(parityPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var modelDir = Environment.GetEnvironmentVariable("EMBEDDING_MODEL_DIR")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "models", "all-MiniLM-L6-v2"));

        using var embedder = new OnnxQueryEmbedder(
            Path.Combine(modelDir, "model.onnx"),
            Path.Combine(modelDir, "vocab.txt"),
            NullLogger<OnnxQueryEmbedder>.Instance);

        var connectionString = TestConfiguration.GetConnectionString();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();

        var repository = new EmbeddingRepository(dataSource, NullLogger<EmbeddingRepository>.Instance);

        foreach (var c in cases)
        {
            // 1) Paridad de embedding: coseno entre C# (ONNX) y Python (sentence-transformers).
            var csharpEmbedding = await embedder.EmbedAsync(c.Question);
            var similarity = CosineSimilarity(csharpEmbedding, c.Vector);
            _output.WriteLine($"'{c.Question}' -> similitud de coseno C#/Python: {similarity:F6}");

            // 2) Paridad de recuperación: mismos IDs con el MISMO vector (el de Python).
            var outcome = await repository.SearchAsync(
                new Vector(c.Vector), c.Question, SearchMode.Hybrid, 10, 20);
            var csharpIds = outcome.Items.Select(r => r.Id).ToList();

            _output.WriteLine($"  Python: [{string.Join(", ", c.Ids)}]");
            _output.WriteLine($"  C#:     [{string.Join(", ", csharpIds)}]");

            Assert.Equal(c.Ids, csharpIds);
            Assert.True(similarity > 0.99, $"Similitud de coseno demasiado baja: {similarity:F6}");
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private sealed record ParityCase(string Question, float[] Vector, int[] Ids);
}
