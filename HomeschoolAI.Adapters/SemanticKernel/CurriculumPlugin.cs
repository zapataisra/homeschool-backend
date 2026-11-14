using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Application.Search;
using HomeschoolAI.Domain.Retrieval;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace HomeschoolAI.Adapters.SemanticKernel;

   /// Plugin nativo de Semantic Kernel.
   /// Expone la búsqueda híbrida sobre el currículo educativo de la SEP/INEA como una
   /// función invocable por el LLM mediante function calling. Este plugin es usado por el Axolotl Guía para fundamentar
   /// sus tutorías con los niños bajo el Método Feynman.
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
    [Description("Busca fragmentos oficiales del currículo educativo de México (SEP/INEA). Úsalo siempre antes de enseñar un concepto nuevo para asegurarte de que tu explicación se basa en el plan de estudios oficial.")]
    public async Task<string> SearchCurriculumAsync(
        [Description("El tema educativo, concepto o pregunta específica que el niño está estudiando y sobre la cual necesitas contexto del plan de estudios.")] string question,
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
