using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using HomeschoolAI.Domain.Conversation;

namespace HomeschoolAI.Adapters.SemanticKernel;

/// <summary>
/// Orquestación de completado con Semantic Kernel. Recibe el mensaje de sistema por
/// parámetro (dinámico), reconstruye el historial y ejecuta el bucle de function
/// calling (<see cref="ToolCallBehavior.AutoInvokeKernelFunctions"/>).
/// </summary>
public sealed partial class SemanticKernelAgentCompletionService : IAgentCompletionService
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelAgentCompletionService> _logger;

    public SemanticKernelAgentCompletionService(Kernel kernel, ILogger<SemanticKernelAgentCompletionService> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(ConversationHistory history, string systemPrompt, CancellationToken cancellationToken = default)
    {
        var chat = _kernel.Services.GetService<IChatCompletionService>()
            ?? throw new InvalidOperationException(
                "No está configurado el chat completion de OpenAI. Define OPENAI_API_KEY.");

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);

        foreach (var message in history.Messages)
        {
            chatHistory.AddMessage(MapRole(message.Role), message.Content);
        }

        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.2
        };

        var response = await chat.GetChatMessageContentAsync(chatHistory, settings, _kernel, cancellationToken);
        var content = response.Content ?? string.Empty;

        LogAgentCompleted(_logger, chatHistory.Count, content.Length);

        return content;
    }

    private static AuthorRole MapRole(ConversationRole role) =>
        role == ConversationRole.User ? AuthorRole.User : AuthorRole.Assistant;

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information,
        Message = "Respuesta del agente generada: {HistoryCount} mensajes en el prompt, {ReplyLength} caracteres")]
    private static partial void LogAgentCompleted(ILogger logger, int historyCount, int replyLength);
}
