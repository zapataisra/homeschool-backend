using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Npgsql;
using Pgvector.Npgsql;
using HomeschoolAI.Adapters.Embedding;
using HomeschoolAI.Adapters.Persistence;
using HomeschoolAI.Adapters.SemanticKernel;
using HomeschoolAI.Application.Chat;
using HomeschoolAI.Application.Search;
using Xunit;
using Xunit.Abstractions;

namespace HomeschoolAI.Tests;

public class CurriculumPluginIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public CurriculumPluginIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Kernel_invokes_search_curriculum_and_answers_with_sep_content()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _output.WriteLine("SKIPPED: define OPENAI_API_KEY para ejecutar la prueba contra OpenAI.");
            return;
        }

        // Motor de búsqueda real: embedder ONNX + repositorio híbrido sobre PostgreSQL (rol app_readonly).
        var modelDir = Environment.GetEnvironmentVariable("EMBEDDING_MODEL_DIR")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "models", "all-MiniLM-L6-v2"));

        using var embedder = new OnnxQueryEmbedder(
            Path.Combine(modelDir, "model.onnx"),
            Path.Combine(modelDir, "vocab.txt"),
            NullLogger<OnnxQueryEmbedder>.Instance);

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(TestConfiguration.GetConnectionString());
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();

        var repository = new EmbeddingRepository(dataSource, NullLogger<EmbeddingRepository>.Instance);
        var searchService = new SearchService(repository, embedder, NullLogger<SearchService>.Instance);

        // Kernel con function calling hacia OpenAI.
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(modelId: "gpt-4o-mini", apiKey: apiKey);
        var kernel = builder.Build();

        var plugin = new CurriculumPlugin(searchService, NullLogger<CurriculumPlugin>.Instance);
        kernel.Plugins.AddFromObject(plugin, "curriculum");

        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var chat = kernel.Services.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(AxolotlPrompts.FeynmanTutorSystemPrompt);
        history.AddUserMessage("¿Qué es el codiseño escolar?");

        var stopwatch = Stopwatch.StartNew();
        var response = await chat.GetChatMessageContentAsync(history, settings, kernel);
        stopwatch.Stop();

        _output.WriteLine("=== RESPUESTA FINAL DEL LLM ===");
        _output.WriteLine(response.Content);
        _output.WriteLine("=== HISTORIAL (evidencia del function calling) ===");
        foreach (var message in history)
        {
            var preview = message.Content ?? string.Join(" | ", message.Items.Select(i => i.ToString()));
            if (preview.Length > 180)
            {
                preview = preview[..180] + "…";
            }

            _output.WriteLine($"[{message.Role}] {preview}");
        }

        _output.WriteLine($"=== LATENCIA END-TO-END: {stopwatch.ElapsedMilliseconds} ms ===");

        Assert.False(string.IsNullOrWhiteSpace(response.Content), "El LLM no devolvió una respuesta final.");
        Assert.Contains(history, m => m.Role == AuthorRole.Tool);
    }
}
