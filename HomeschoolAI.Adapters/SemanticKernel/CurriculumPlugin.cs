using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Application.Search;
using HomeschoolAI.Domain.Retrieval;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace HomeschoolAI.Adapters.SemanticKernel;

/// <summary>
/// Plugin nativo de Semantic Kernel: el "Agente Bibliotecario" de LUMI.
/// Expone la búsqueda híbrida sobre los Programas Sintéticos de la SEP como una
/// función invocable por el LLM mediante function calling.
/// </summary>
public sealed class CurriculumPlugin
{
    private readonly ISearchService _searchService;
    private readonly ILogger<CurriculumPlugin> _logger;

    public CurriculumPlugin(ISearchService searchService, ILogger<CurriculumPlugin> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    [KernelFunction("search_curriculum")]
    [Description("Busca fragmentos de los Programas Sintéticos de la SEP (Fases 3, 4 y 5) relevantes para una pregunta sobre el currículo de educación primaria. Devuelve el texto curricular citando su fuente (PDF y fragmento).")]
    public async Task<string> SearchCurriculumAsync(
        [Description("Pregunta o tema del docente sobre el currículo de primaria que se desea fundamentar.")] string question,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = await _searchService.SearchAsync(
            new SearchQuery
            {
                Question = question,
                Mode = SearchMode.Hybrid,
                Limit = 5,
                Pool = 20
            },
            cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation(
            "search_curriculum invocada por el LLM: {ResultCount} fragmentos en {ElapsedMs}ms (longitud de pregunta {QuestionLength})",
            results.Count,
            stopwatch.ElapsedMilliseconds,
            question.Length);

        if (results.Count == 0)
        {
            return "No se encontraron fragmentos curriculares relevantes en los Programas Sintéticos.";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < results.Count; i++)
        {
            var chunk = results[i];
            builder.AppendLine($"[{i + 1}] {chunk.Source} · fragmento {chunk.ChunkId}");
            builder.AppendLine(chunk.Texto);
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
