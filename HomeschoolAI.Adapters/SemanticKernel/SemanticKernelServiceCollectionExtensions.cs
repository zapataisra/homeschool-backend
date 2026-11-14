using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using HomeschoolAI.Domain.Conversation;

namespace HomeschoolAI.Adapters.SemanticKernel;

/// <summary>
/// Registro de dependencias de Semantic Kernel: el plugin de currículo
/// (<c>search_curriculum</c>), el orquestador del agente y el <see cref="Kernel"/>
/// con el chat completion de OpenAI.
/// </summary>
public static class SemanticKernelServiceCollectionExtensions
{
    public static IServiceCollection AddHomeschoolSemanticKernel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var openAiApiKey = configuration["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var openAiModelId = configuration["OpenAI:ModelId"]
            ?? Environment.GetEnvironmentVariable("OPENAI_MODEL_ID")
            ?? "gpt-4o-mini";

        // "Manos" del agente: plugin nativo que expone la búsqueda híbrida (RRF) como
        // función invocable por el LLM. Llama al puerto de entrada ISearchService.
        services.AddSingleton<CurriculumPlugin>();

        // Orquestador: reconstruye el historial y ejecuta el bucle de function calling.
        services.AddSingleton<IAgentCompletionService, SemanticKernelAgentCompletionService>();

        // "Cerebro": Kernel Singleton con el chat completion y el plugin de currículo.
        services.AddSingleton<Kernel>(sp =>
        {
            var kernelBuilder = Kernel.CreateBuilder();

            if (!string.IsNullOrWhiteSpace(openAiApiKey))
            {
                kernelBuilder.AddOpenAIChatCompletion(modelId: openAiModelId, apiKey: openAiApiKey);
            }
            else
            {
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("SemanticKernel")
                    .LogWarning("OPENAI_API_KEY no está configurada: el endpoint /api/agent/chat responderá con error hasta que se defina.");
            }

            var kernel = kernelBuilder.Build();
            kernel.Plugins.AddFromObject(sp.GetRequiredService<CurriculumPlugin>(), "curriculum");
            return kernel;
        });

        return services;
    }
}
