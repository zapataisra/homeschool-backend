using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using HomeschoolAI.Adapters.Embedding;
using Xunit;
using Xunit.Abstractions;

namespace HomeschoolAI.Tests;

public class EmbeddingGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public EmbeddingGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Embed_question_returns_384_dimensions()
    {
        var modelDir = Environment.GetEnvironmentVariable("EMBEDDING_MODEL_DIR")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "models", "all-MiniLM-L6-v2"));

        var modelPath = Path.Combine(modelDir, "model.onnx");
        var vocabPath = Path.Combine(modelDir, "vocab.txt");

        using var embedder = new OnnxQueryEmbedder(modelPath, vocabPath, NullLogger<OnnxQueryEmbedder>.Instance);

        var stopwatch = Stopwatch.StartNew();
        var vector = await embedder.EmbedAsync("¿Qué es un PDA?");
        stopwatch.Stop();

        _output.WriteLine($"Dimensión del embedding: {vector.Length}");
        _output.WriteLine($"Latencia de inferencia: {stopwatch.ElapsedMilliseconds} ms");

        Assert.Equal(384, vector.Length);
        Assert.NotEmpty(vector);
    }
}
