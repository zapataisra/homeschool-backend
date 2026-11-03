using System.Diagnostics;
using Microsoft.Extensions.Logging;
using HomeschoolAI.Application.Ports;
using HomeschoolAI.Application.Telemetry;
using HomeschoolAI.Domain.Conversation;

namespace HomeschoolAI.Application.Chat;

/// <summary>
/// Caso de uso de chat: gestiona el historial de la conversación (hilo),
/// invoca al agente y persiste los turnos de usuario y asistente.
/// </summary>
public sealed partial class ChatService : IChatService
{
    /// <summary>Tope de mensajes persistidos por hilo (10 turnos) para acotar coste y memoria.</summary>
    private const int MaxHistoryMessages = 20;

    private readonly IConversationStore _store;
    private readonly IAgentCompletionService _completion;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IConversationStore store, IAgentCompletionService completion, ILogger<ChatService> logger)
    {
        _store = store;
        _completion = completion;
        _logger = logger;
    }

    public async Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = ApplicationTelemetry.ActivitySource.StartActivity("chat");
        activity?.SetTag("chat.message_length", request.Message.Length);

        var isNewThread = string.IsNullOrWhiteSpace(request.ThreadId);
        var threadId = isNewThread ? Guid.NewGuid().ToString("N") : request.ThreadId!.Trim();

        var history = await _store.GetOrCreateAsync(threadId, cancellationToken);

        var withUser = history with
        {
            Messages = history.Messages.Append(new ConversationMessage(ConversationRole.User, request.Message)).ToList()
        };

        var stopwatch = Stopwatch.StartNew();
        var reply = await _completion.CompleteAsync(withUser, cancellationToken);
        stopwatch.Stop();

        var messages = withUser.Messages.Append(new ConversationMessage(ConversationRole.Assistant, reply)).ToList();
        var withAssistant = withUser with { Messages = Trim(messages) };

        await _store.SaveAsync(withAssistant, cancellationToken);

        LogChatCompleted(_logger, isNewThread, request.Message.Length, reply.Length, stopwatch.ElapsedMilliseconds);
        activity?.SetTag("chat.new_thread", isNewThread);
        activity?.SetTag("chat.reply_length", reply.Length);

        return new ChatResponse(threadId, reply);
    }

    private static List<ConversationMessage> Trim(List<ConversationMessage> messages)
    {
        if (messages.Count <= MaxHistoryMessages)
        {
            return messages;
        }

        return messages.Skip(messages.Count - MaxHistoryMessages).ToList();
    }

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Chat completado: hilo nuevo {IsNewThread}, longitud de mensaje {MessageLength}, longitud de respuesta {ReplyLength}, duración {ElapsedMs}ms")]
    private static partial void LogChatCompleted(ILogger logger, bool isNewThread, int messageLength, int replyLength, long elapsedMs);
}
