using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using HomeschoolAI.Domain.Embedding;

namespace HomeschoolAI.Adapters.Embedding;

/// <summary>
/// Generador local de embeddings con ONNX Runtime + <c>all-MiniLM-L6-v2</c>.
/// Tokeniza, infiere y aplica mean pooling para devolver un vector de 384 dimensiones.
/// </summary>
public sealed partial class OnnxQueryEmbedder : IQueryEmbedder, IDisposable
{
    private const int EmbeddingDimension = 384;
    private const int ClsTokenId = 101;   // [CLS] en el vocab de BERT base
    private const int SepTokenId = 102;   // [SEP] en el vocab de BERT base

    private readonly InferenceSession _session;
    private readonly Tokenizer _tokenizer;
    private readonly ILogger<OnnxQueryEmbedder> _logger;

    public OnnxQueryEmbedder(string modelPath, string vocabPath, ILogger<OnnxQueryEmbedder> logger)
    {
        _logger = logger;
        _session = new InferenceSession(modelPath);
        _tokenizer = BertTokenizer.Create(vocabPath, new BertOptions
        {
            LowerCaseBeforeTokenization = true,   // all-MiniLM-L6-v2 es uncased
            ApplyBasicTokenization = true
        });
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ids = _tokenizer.EncodeToIds(text);
        var sequenceLength = ids.Count + 2;   // [CLS] + tokens + [SEP]

        var inputIds = new long[sequenceLength];
        var attentionMask = new long[sequenceLength];
        var tokenTypeIds = new long[sequenceLength];

        inputIds[0] = ClsTokenId;
        attentionMask[0] = 1L;
        tokenTypeIds[0] = 0L;

        for (var i = 0; i < ids.Count; i++)
        {
            inputIds[i + 1] = ids[i];
            attentionMask[i + 1] = 1L;   // sin padding
            tokenTypeIds[i + 1] = 0L;    // una sola oración
        }

        inputIds[sequenceLength - 1] = SepTokenId;
        attentionMask[sequenceLength - 1] = 1L;
        tokenTypeIds[sequenceLength - 1] = 0L;

        var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, sequenceLength });
        var attentionTensor = new DenseTensor<long>(attentionMask, new[] { 1, sequenceLength });
        var tokenTypeTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, sequenceLength });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeTensor),
        };

        var stopwatch = Stopwatch.StartNew();
        using var results = _session.Run(inputs);
        var flat = results.First(r => r.Name == "last_hidden_state").AsTensor<float>().ToArray();
        stopwatch.Stop();

        // Mean pooling sobre la dimensión de secuencia (attention_mask = 1).
        var embedding = new float[EmbeddingDimension];
        for (var i = 0; i < sequenceLength; i++)
        {
            var offset = i * EmbeddingDimension;
            for (var j = 0; j < EmbeddingDimension; j++)
            {
                embedding[j] += flat[offset + j];
            }
        }

        for (var j = 0; j < EmbeddingDimension; j++)
        {
            embedding[j] /= sequenceLength;
        }

        LogEmbeddingGenerated(_logger, EmbeddingDimension, sequenceLength, stopwatch.ElapsedMilliseconds);

        return Task.FromResult(embedding);
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Debug,
        Message = "Embedding generado: dimensiones {Dimensions}, tokens {TokenCount}, duración {ElapsedMs}ms")]
    private static partial void LogEmbeddingGenerated(ILogger logger, int dimensions, int tokenCount, long elapsedMs);
}
