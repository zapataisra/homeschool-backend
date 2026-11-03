using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using HomeschoolAI.Adapters.Embedding;
using Xunit;
using Xunit.Abstractions;

namespace HomeschoolAI.Tests;

public class EmbeddingGeneratorTests : IClassFixture<EmbeddingFixture>
{
    private readonly EmbeddingFixture _fixture;
    private readonly ITestOutputHelper _output;

    public EmbeddingGeneratorTests(EmbeddingFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Embed_question_returns_384_dimensions()
    {
        var stopwatch = Stopwatch.StartNew();
        var vector = await _fixture.Embedder.EmbedAsync("¿Qué es un PDA?");
        stopwatch.Stop();

        _output.WriteLine($"Dimensión del embedding: {vector.Length}");
        _output.WriteLine($"Latencia de inferencia: {stopwatch.ElapsedMilliseconds} ms");

        Assert.Equal(384, vector.Length);
        Assert.NotEmpty(vector);
    }

    [Fact]
    public async Task Embed_is_deterministic_and_non_degenerate()
    {
        var v1 = await _fixture.Embedder.EmbedAsync("¿Qué es un PDA?");
        var v2 = await _fixture.Embedder.EmbedAsync("¿Qué es un PDA?");
        var other = await _fixture.Embedder.EmbedAsync("Matemáticas y operaciones básicas");

        Assert.Equal(384, v1.Length);
        Assert.True(v1.SequenceEqual(v2), "El embedding debe ser determinista para el mismo texto.");
        Assert.Contains(v1, x => Math.Abs(x) > 1e-6f);
        Assert.All(v1, x => Assert.False(float.IsNaN(x)));
        Assert.False(v1.SequenceEqual(other), "Textos distintos deben producir embeddings distintos.");
    }
}

public sealed class EmbeddingFixture : IDisposable
{
    public OnnxQueryEmbedder Embedder { get; }

    public EmbeddingFixture()
    {
        var modelDir = Environment.GetEnvironmentVariable("EMBEDDING_MODEL_DIR")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "models", "all-MiniLM-L6-v2"));

        Embedder = new OnnxQueryEmbedder(
            Path.Combine(modelDir, "model.onnx"),
            Path.Combine(modelDir, "vocab.txt"),
            NullLogger<OnnxQueryEmbedder>.Instance);
    }

    public void Dispose() => Embedder.Dispose();
}
